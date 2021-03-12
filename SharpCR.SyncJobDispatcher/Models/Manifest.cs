namespace SharpCR.SyncJobDispatcher.Models
{
    public class ProbedManifest
    {
        public byte[] Bytes { get; set; }
        public string MediaType { get; set; }
        
        public long Size { get; set; }
    }
}