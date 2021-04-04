namespace SharpCR.JobDispatcher.Models
{
    public class JobListResult
    {
        public Job[] QueuedJobs { get; set; }
        public Job[] SyncingJobs { get; set; }
    }
}