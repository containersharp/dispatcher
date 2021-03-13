using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharpCR.SyncJobDispatcher.Models;
using SharpCR.SyncJobDispatcher.Services;

namespace SharpCR.SyncJobDispatcher.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly ILogger<JobsController> _logger;
        private readonly ConcurrentQueue<Job> _theJobQueue;
        private readonly ManifestProber _prober;

        public JobsController(ILogger<JobsController> logger,  ConcurrentQueue<Job> theJobQueue, ManifestProber prober)
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
            
            var upstreamManifest = await _prober.ProbeManifestAsync(syncJob);
            if (upstreamManifest == null)
            {
                _logger.LogInformation("No manifest found for request: @job", syncJob.ToPublicModel());
                return NotFound();
            }

            syncJob.Id = Guid.NewGuid().ToString("N");
            syncJob.Size = upstreamManifest.Size;
            _theJobQueue.Enqueue(syncJob);

            _logger.LogInformation("Sync request queued: @job", syncJob.ToPublicModel());
            var manifestStream = new MemoryStream(upstreamManifest.Bytes);
            return new FileStreamResult(manifestStream, upstreamManifest.MediaType);
        }

        [HttpGet]
        public IEnumerable<Job> Get()
        {
            return _theJobQueue.Select(job => job.ToPublicModel()).ToArray();
        }
    }
}
