namespace SharpCR.JobDispatcher
{
    // ReSharper disable once InconsistentNaming
    public class DispatcherConfig
    {
        public int MaxWorkerPollMinutes { get; set; } = 3;

        public int LowestSyncSpeedKbps { get; set; } = 200;

        public int MaxTrails { get; set; } = 3;
        
        public string AuthorizationToken { get; set; }

    }
}