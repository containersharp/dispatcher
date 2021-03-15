using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private readonly ManifestProber _prober;

        public JobsController(ILogger<JobsController> logger,  JobProducerConsumerQueue theJobQueue, ManifestProber prober)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
            _prober = prober;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Job syncJob)
        {
            if (syncJob == null || string.IsNullOrEmpty(syncJob.ImageRepository))
            {
                _logger.LogWarning("Ignoring request: no valid sync job object found.");
                return NotFound();
            }

            var upstreamManifest =  await _prober.ProbeManifestAsync(syncJob);
            if (upstreamManifest == null)
            {
                _logger.LogInformation("No manifest found for request: @job", syncJob.ToPublicModel());
                return NotFound();
            }
            
            foreach (var manifestItem in upstreamManifest.ManifestItems)
            {
                var actualJob = new Job
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Size = manifestItem.LayerTotalSize(),
                    Digest = manifestItem.Digest,
                    AuthorizationToken = syncJob.AuthorizationToken
                    // ImageRepository = 
                };
                _theJobQueue.AddJob(actualJob);
                _logger.LogInformation("Sync request queued: @job", actualJob.ToPublicModel());
            }
            return Content(upstreamManifest.ToJsonString(), "application/json");
        }

        [HttpGet]
        public IEnumerable<Job> Get()
        {
            return _theJobQueue.GetPendingJobs().Select(job => job.ToPublicModel()).ToArray();
        }
    }
}
