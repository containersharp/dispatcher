using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCR.SyncJobDispatcher.Models;

namespace SharpCR.SyncJobDispatcher
{
    public class Sweeper
    {
        private readonly ConcurrentQueue<Job> _theJobQueue;
        private readonly List<Job> _theWorkingList;
        private readonly DispatcherConfig _config;
        private bool _stopped = false;
        
        public Sweeper(ILogger<Sweeper> logger, IOptions<DispatcherConfig> dispatcherOptions, ConcurrentQueue<Job> theJobQueue, List<Job> theWorkingList)
        {
            _theJobQueue = theJobQueue;
            _theWorkingList = theWorkingList;
            _config = dispatcherOptions.Value;
        }

        public void Start()
        {
            _stopped = false;
            while (!_stopped)
            {
                var now = DateTime.UtcNow;
                var indexesToRemove = new List<int>();
                
                for (var index = _theWorkingList.Count - 1 ; index >= 0; index--)
                {
                    var job = _theWorkingList[index];
                    var lastTrial = job.Trails.OrderByDescending(t => t.StartTime).First();
                    var maxSeconds = job.Size / (_config.LowestSyncSpeedKbps * 1024);
                    var elapsed = now - lastTrial.StartTime;
                    
                    if (elapsed.TotalSeconds > maxSeconds)
                    {
                        indexesToRemove.Add(index);
                        if (job.Trails.Count < _config.MaxTrails)
                        {
                            _theJobQueue.Enqueue(job);
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
            _stopped = true;
        }
    }
}