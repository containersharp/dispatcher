using System.Collections.Generic;
using SharpCR.JobDispatcher.Models;

namespace SharpCR.JobDispatcher.Services
{
    public class JobWorkingList
    {
        private readonly object _locker = new object();
        private readonly List<Job> _workingJobs = new List<Job>();

        public void Add(Job job)
        {
            lock (_locker)
            {
                _workingJobs.Add(job);
            }
        }
        
        public Job RemoveByJobId(string id)
        {
            lock (_locker)
            {
                for (var i = _workingJobs.Count - 1; i >= 0; i--)
                {
                    var job = _workingJobs[i];
                    if (job.Id == id)
                    {
                        _workingJobs.RemoveAt(i);
                        return job;
                    }
                }
            }

            return null;
        }  
        
        public void RemoveByJobIdList(HashSet<string> ids)
        {
            lock (_locker)
            {
                for (var i = _workingJobs.Count - 1; i >= 0; i--)
                {
                    if (ids.Contains(_workingJobs[i].Id))
                    {
                        _workingJobs.RemoveAt(i);
                    }
                }
            }
        }
        
        public Job[] Snapshot()
        {
            lock(_locker)
            {
                var jobs = new Job[_workingJobs.Count];
                _workingJobs.CopyTo(jobs);
                return jobs;
            }
        }
    }
}