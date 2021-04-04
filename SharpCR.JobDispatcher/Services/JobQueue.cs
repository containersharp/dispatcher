using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using SharpCR.JobDispatcher.Models;


namespace SharpCR.JobDispatcher.Services
{
    public sealed class JobQueue : IDisposable
    {
        private readonly CancellationTokenSource _closeSignal;
        private readonly BlockingCollection<Func<Job, bool>> _workerQueue;
        private readonly BlockingCollection<Job> _jobQueue;
        private Job _processingJob;

        public JobQueue(CancellationTokenSource closeSignal)
        {
            _closeSignal = closeSignal;
            _workerQueue = new BlockingCollection<Func<Job, bool>>(new ConcurrentQueue<Func<Job, bool>>());
            _jobQueue = new BlockingCollection<Job>(new ConcurrentQueue<Job>());
            
            new Thread(() => ConsumeLoop(closeSignal.Token)).Start();
        }

        public void AddJob(Job value)
        {
            _jobQueue.Add(value);
        }
        
        public Job[] GetPendingJobs()
        {
            var jobs = new Job[_jobQueue.Count + 1];
            _jobQueue.CopyTo(jobs, 1);
            if (_processingJob != null)
            {
                jobs[0] = _processingJob;
                return jobs;
            }
            return jobs.Skip(1).ToArray();
        }
        
        public void AddWorker(Func<Job, bool> worker)
        {
            _workerQueue.Add(worker);
        }
        
        private void ConsumeLoop(CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    var job = _jobQueue.Take(cancelToken);
                    _processingJob = job;
                    Func<Job, bool> validWorker;
                    do
                    {
                        validWorker = _workerQueue.Take(cancelToken);
                    } while (!validWorker(job));
                    _processingJob = null;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        #region IDisposable

        private bool _isDisposed;

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _closeSignal.Cancel();
                    _closeSignal.Dispose();
                    _jobQueue.Dispose();
                    _workerQueue.Dispose();
                }

                _isDisposed = true;
            }
        }
    
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }

}