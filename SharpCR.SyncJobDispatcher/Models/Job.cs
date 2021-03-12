using System.Collections.Generic;

namespace SharpCR.SyncJobDispatcher.Models
{
    public class Job
    {
        public string Id { get; set; }
        
        public string ImageRepository { get; set; }
        
        public string Tag { get; set; }
        
        public string Digest { get; set; }
        
        public string AuthorizationToken { get; set; }
        
        public long Size { get; set; }

        public List<Trail> Trails { get; set; } = new List<Trail>();
        
        
        public Job ToPublicModel()
        {
            return new Job
            {
                Id = this.Id,
                ImageRepository = this.ImageRepository,
                Tag = this.Tag,
                Digest = this.Digest,
                Trails = this.Trails
            };
        }
    }
}