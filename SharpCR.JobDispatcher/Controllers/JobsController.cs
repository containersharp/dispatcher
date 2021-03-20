using System;
using System.Collections.Generic;
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
        private readonly JobProducerConsumerQueue _theJobQueue;

        public JobsController(ILogger<JobsController> logger,  JobProducerConsumerQueue theJobQueue)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
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

            syncJob.Id = Guid.NewGuid().ToString("N");
            _theJobQueue.AddJob(syncJob);
            _logger.LogInformation("Sync request queued: {@job}", syncJob.ToPublicModel());
        }

        [HttpGet]
        public IEnumerable<Job> Get()
        {
            return _theJobQueue.GetPendingJobs().Select(job => job.ToPublicModel()).ToArray();
        }
    }
}
