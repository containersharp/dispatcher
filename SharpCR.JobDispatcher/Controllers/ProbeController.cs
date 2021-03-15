using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharpCR.JobDispatcher.Models;
using SharpCR.JobDispatcher.Services;

namespace SharpCR.JobDispatcher.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProbeController: ControllerBase
    {
        private readonly ILogger<ProbeController> _logger;
        private readonly ManifestProber _prober;

        public ProbeController(ILogger<ProbeController> logger, ManifestProber prober)
        {
            _logger = logger;
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

            return Content(upstreamManifest.ToJsonString(), "application/json");
        }
    }
}