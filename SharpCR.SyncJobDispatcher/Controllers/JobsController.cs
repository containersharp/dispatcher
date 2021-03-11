using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SharpCR.SyncJobDispatcher.Models;

namespace SharpCR.SyncJobDispatcher.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JobsController : ControllerBase
    {
        private readonly ILogger<JobsController> _logger;
        private readonly ConcurrentQueue<Job> _theJobQueue;

        public JobsController(ILogger<JobsController> logger, ConcurrentQueue<Job> theJobQueue)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
        }

        // 1. 接收来自境内的 post /jobs 请求，尝试检查上游仓库中是否存在指定的镜像，如果有，则返回 manifest，并立即使 job 入队，等待同步
        public IActionResult Post([FromBody] Job syncJob)
        {
            // 1. todo: check if it exists
            syncJob.Id = Guid.NewGuid().ToString("N");
            _theJobQueue.Enqueue(syncJob);
            return Ok();
        }

        public IEnumerable<Job> Get()
        {
            return _theJobQueue.Select(job => job.ToPublicModel()).ToArray();
        }
    }
}
