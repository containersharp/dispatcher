namespace SharpCR.JobDispatcher
{
    // ReSharper disable once InconsistentNaming
    public class DispatcherConfig
    {
        public int LowestSyncSpeedKbps { get; set; } = 50;

        public int MaxTrails { get; set; } = 3;
        
        public string AuthorizationToken { get; set; }

    }
}