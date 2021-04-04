namespace SharpCR.JobDispatcher
{
    // ReSharper disable once InconsistentNaming
    public class DispatcherConfig
    {
        public int LowestSyncSpeedKbps { get; set; } = 50;

        public int MaxTrails { get; set; } = 3;
        
        public string AuthorizationToken { get; set; }

        public RegistryCredential[] BuiltinCredentials { get; set; } = new RegistryCredential[0];
    }

    public class RegistryCredential
    {
        public string Hostname { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}