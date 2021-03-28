using System.Collections.Generic;
using System.Linq;
using SharpCR.Manifests;

namespace SharpCR.JobDispatcher.Models
{
    public class ProbedResult
    {
        public ManifestV2List ListManifest { get; set; }
        public Manifest[] ManifestItems { get; set; }

        public Packed ToPacked()
        {
            return new Packed
            {
                ListManifest = this.ListManifest?.RawJsonBytes,
                ManifestItems = this.ManifestItems?.Select(i => i.RawJsonBytes).ToList()
            };
        }


        public class Packed
        {
            public byte[] ListManifest { get; set; }
            public List<byte[]> ManifestItems { get; set; }
        }
    }
}