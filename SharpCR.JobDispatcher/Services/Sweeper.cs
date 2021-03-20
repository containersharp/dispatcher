using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCR.JobDispatcher.Models;

namespace SharpCR.JobDispatcher.Services
{
    public class Sweeper
    {
        private readonly ILogger<Sweeper> _logger;
        private readonly JobProducerConsumerQueue _theJobQueue;
        private readonly List<Job> _theWorkingList;
        private readonly DispatcherConfig _config;
        private bool _stopped = false;
        
        public Sweeper(ILogger<Sweeper> logger, IOptions<DispatcherConfig> dispatcherOptions, JobProducerConsumerQueue theJobQueue, List<Job> theWorkingList)
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
                var indexesToRemove = new List<int>();

                for (var index = _theWorkingList.Count - 1; index >= 0; index--)
                {
                    var job = _theWorkingList[index];
                    var lastTrial = job.Trails.OrderByDescending(t => t.StartTime).First();
                    var maxSeconds = Math.Max((job.Size ?? 0) / (_config.LowestSyncSpeedKbps * 1024), 5);
                    var elapsed = now - lastTrial.StartTime;

                    if (elapsed.TotalSeconds > maxSeconds)
                    {
                        var trailsCount = job.Trails.Count;
                        var tryAgain = trailsCount < _config.MaxTrails;
                        _logger.LogWarning(
                            "Job {job} has exceeded its longest waiting time ({totalSeconds}s > {maxSeconds}s), this is the {trail} time. try again: {tryAgain}",
                            job.ToPublicModel(), elapsed.TotalSeconds, trailsCount, maxSeconds, tryAgain);
                        indexesToRemove.Add(index);
                        if (tryAgain)
                        {
                            _logger.LogWarning("Retrying job {@job}", job.ToPublicModel());
                            _theJobQueue.AddJob(job);
                        }
                    }
                }

                foreach (var index in indexesToRemove)
                {
                    _theWorkingList.RemoveAt(index);
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }

        public void Stop()
        {
            _logger.LogInformation("Stopping the sweeper.");
            _stopped = true;
        }
    }
}