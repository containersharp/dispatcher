using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharpCR.JobDispatcher.Models;
using SharpCR.JobDispatcher.Services;

namespace SharpCR.JobDispatcher.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly ILogger<JobsController> _logger;
        private readonly JobQueue _theJobQueue;
        private readonly JobWorkingList _workingList;

        public JobsController(ILogger<JobsController> logger,  JobQueue theJobQueue, JobWorkingList workingList)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
            _workingList = workingList;
        }

        [HttpPost]
        public IActionResult Post([FromBody] Job[] batch)
        {
            if (batch == null)
            {
                return NotFound();
            }

            foreach (var job in batch)
            {
                ScheduleJob(job);
            }
            return Accepted();
        }

        private void ScheduleJob(Job syncJob)
        {
            var noRepo = syncJob == null || string.IsNullOrEmpty(syncJob.ImageRepository) ||
                         (string.IsNullOrEmpty(syncJob.Digest) && string.IsNullOrEmpty(syncJob.Tag));
            if (noRepo)
            {
                _logger.LogWarning("Ignoring job {@job}: no valid sync job object found.", syncJob?.ToPublicModel());
                return;
            }
            
            var pendingJobs = _theJobQueue.GetPendingJobs().Concat(_workingList.Snapshot()).ToArray();
            if(pendingJobs.Any(j =>
                string.Equals( j.ImageRepository, syncJob.ImageRepository, StringComparison.OrdinalIgnoreCase)
                && string.Equals( j.Tag, syncJob.Tag, StringComparison.OrdinalIgnoreCase)
                && string.Equals( j.Digest, syncJob.Digest, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("Ignoring job {@job}: duplicated job detected.", syncJob.ToPublicModel());
                return;
            }

            syncJob.Id = Guid.NewGuid().ToString("N");
            _theJobQueue.AddJob(syncJob);
            _logger.LogInformation("Sync request queued: {@job}", syncJob.ToPublicModel());
        }

        [HttpGet]
        public JobListResult Get()
        {
            var queuedJobs = _theJobQueue.GetPendingJobs().Select(job => job.ToPublicModel()).ToArray();
            var syncingJobs = _workingList.Snapshot().Select(job => job.ToPublicModel()).ToArray();

            return new JobListResult
            {
                QueuedJobs = queuedJobs,
                SyncingJobs = syncingJobs
            };
        }
    }
}
