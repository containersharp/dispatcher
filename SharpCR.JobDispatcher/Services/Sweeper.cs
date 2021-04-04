using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharpCR.JobDispatcher.Services
{
    public class Sweeper
    {
        private readonly ILogger<Sweeper> _logger;
        private readonly JobQueue _theJobQueue;
        private readonly JobWorkingList _theWorkingList;
        private readonly DispatcherConfig _config;
        private bool _stopped = false;
        
        public Sweeper(ILogger<Sweeper> logger, IOptions<DispatcherConfig> dispatcherOptions, JobQueue theJobQueue, JobWorkingList theWorkingList)
        {
            _logger = logger;
            _theJobQueue = theJobQueue;
            _theWorkingList = theWorkingList;
            _config = dispatcherOptions.Value;
        }

        public void Start()
        {
            _logger.LogInformation("Starting the sweeper...");
            new Thread(StartSweeping).Start();
        }

        private void StartSweeping()
        {
            _stopped = false;
            while (!_stopped)
            {
                var now = DateTime.UtcNow;
                var workingJobs = _theWorkingList.Snapshot();
                var jobIdsToRemove = new List<string>();

                for (var index = 0; index < workingJobs.Length; index++)
                {
                    var job = workingJobs[index];
                    var lastTrial = job.Trails.OrderByDescending(t => t.StartTime).First();
                    var maxSeconds = Math.Max((job.Size ?? 0) / (_config.LowestSyncSpeedKbps * 1024), 300);
                    var elapsed = now - lastTrial.StartTime;

                    if (elapsed.TotalSeconds > maxSeconds)
                    {
                        var trailsCount = job.Trails.Count;
                        var tryAgain = trailsCount < _config.MaxTrails;
                        _logger.LogWarning("Job {job} has exceeded its longest waiting time ({totalSeconds}s > {maxSeconds}s), this is the {trail} time. try again: {tryAgain}",
                            job.ToPublicModel(), elapsed.TotalSeconds, maxSeconds, trailsCount, tryAgain);
                        jobIdsToRemove.Add(job.Id);
                        if (tryAgain)
                        {
                            _logger.LogWarning("Retrying job {@job}", job.ToPublicModel());
                            _theJobQueue.AddJob(job);
                        }
                    }
                }

                _theWorkingList.RemoveByJobIdList(jobIdsToRemove.ToHashSet());
                Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        public void Stop()
        {
            _logger.LogInformation("Stopping the sweeper.");
            _stopped = true;
        }
    }
}