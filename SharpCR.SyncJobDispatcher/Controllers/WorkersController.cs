using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCR.SyncJobDispatcher.Models;

namespace SharpCR.SyncJobDispatcher.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class WorkersController : ControllerBase
    {
        private readonly ILogger<WorkersController> _logger;
        private readonly ConcurrentQueue<Job> _theJobQueue;
        private readonly List<Job> _theWorkingList;
        private readonly DispatcherConfig _config;

        public WorkersController(ILogger<WorkersController> logger,
            IOptions<DispatcherConfig> dispatcherOptions,
            ConcurrentQueue<Job> theJobQueue, List<Job> theWorkingList)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
            _theWorkingList = theWorkingList;
            _config = dispatcherOptions.Value;
        }

        public Job Post([FromForm] string worker, [FromForm] string jobId)
        {
            if (string.IsNullOrEmpty(worker))
            {
                return null;
            }
            
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            if (!string.IsNullOrEmpty(jobId))
            {
                var job = _theWorkingList.Find(j => j.Id == jobId);
                if (job != null)
                {
                    _theWorkingList.Remove(job);
                }
            }
            
            while (stopwatch.Elapsed.Minutes < _config.MaxWorkerPollMinutes)
            {
                if (_theJobQueue.TryDequeue(out var job))
                {
                    job.Trails.Add(new Trail{ StartTime = DateTime.UtcNow, WorkerHost = worker});
                    _theWorkingList.Add(job);
                    stopwatch.Stop();
                    return job;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(50));
            }
            return null;
        }
    }
}