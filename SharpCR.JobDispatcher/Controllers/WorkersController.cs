using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCR.JobDispatcher.Models;
using SharpCR.JobDispatcher.Services;

namespace SharpCR.JobDispatcher.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class WorkersController : ControllerBase
    {
        private readonly ILogger<WorkersController> _logger;
        private readonly JobProducerConsumerQueue _theJobQueue;
        private readonly List<Job> _theWorkingList;
        private readonly DispatcherConfig _config;

        public WorkersController(ILogger<WorkersController> logger,
            IOptions<DispatcherConfig> dispatcherOptions,
            JobProducerConsumerQueue theJobQueue, List<Job> theWorkingList)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
            _theWorkingList = theWorkingList;
            _config = dispatcherOptions.Value;
        }

        [HttpPost]
        public async Task<Job> Post([FromForm] string worker, [FromForm] string jobId, [FromForm] int? result)
        {
            if (string.IsNullOrEmpty(worker))
            {
                return null;
            }
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (!string.IsNullOrEmpty(jobId))
            {
                HandleTrailResult(worker, jobId, result);
            }

            var delayTask = Task.Delay(TimeSpan.FromMinutes(3));
            var jobAssignmentTask = new TaskCompletionSource<Job>();
            _theJobQueue.AddWorker(job =>
            {
                if (delayTask.Status == TaskStatus.RanToCompletion)
                {
                    delayTask.Dispose();
                    delayTask = null;
                    jobAssignmentTask = null;
                    return false;
                }
                
                jobAssignmentTask.SetResult(job);
                return true;
            });
            
            await Task.WhenAny(jobAssignmentTask.Task, delayTask);
            var assigningJob = jobAssignmentTask.Task.Status == TaskStatus.RanToCompletion ? jobAssignmentTask.Task.Result : null;
            if (null != assigningJob)
            {
                assigningJob.Trails.Add(new Trail{StartTime = DateTime.UtcNow, WorkerHost = worker});
                _theWorkingList.Add(assigningJob);
                _logger.LogInformation("Assigning to worker {@worker}: job: {@job}", worker, assigningJob.ToPublicModel());
            }
            return assigningJob;
        }
        
        

        private void HandleTrailResult(string worker, string jobId, int? result)
        {
            var job = _theWorkingList.Find(j => j.Id == jobId);
            if (job == null) return;
            _theWorkingList.Remove(job);

            if (result == 0)
            {
                _logger.LogInformation("Job {@job} successfully synced by worker {@worker}.", job.ToPublicModel(), worker);
                return;
            }
            
            var tryAgain = job.Trails.Count < _config.MaxTrails;
            _logger.LogWarning("Job {@job} has failed from worker {worker}. Try again: {@tryAgain}", job.ToPublicModel(), worker, tryAgain);
            if (tryAgain)
            {
                _theJobQueue.AddJob(job);
            }
        }
    }
}
