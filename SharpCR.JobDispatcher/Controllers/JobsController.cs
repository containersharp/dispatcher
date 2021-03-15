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
        public IActionResult Post([FromBody] Job syncJob)
        {
            var noRepo = syncJob == null || string.IsNullOrEmpty(syncJob.ImageRepository) || (string.IsNullOrEmpty(syncJob.Tag) && string.IsNullOrEmpty(syncJob.Digest));
            if (noRepo)
            {
                _logger.LogWarning("Ignoring request: no valid sync job object found.");
                return NotFound();
            }

            syncJob.Id = Guid.NewGuid().ToString("N");
            _theJobQueue.AddJob(syncJob);
            _logger.LogInformation("Sync request queued: @job", syncJob.ToPublicModel());
            return Accepted();
        }

        [HttpGet]
        public IEnumerable<Job> Get()
        {
            return _theJobQueue.GetPendingJobs().Select(job => job.ToPublicModel()).ToArray();
        }
    }
}
