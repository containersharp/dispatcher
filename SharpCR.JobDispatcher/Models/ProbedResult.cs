using System.Collections.Generic;
using System.Linq;
using SharpCR.JobDispatcher.Services;
using SharpCR.Manifests;

namespace SharpCR.JobDispatcher.Models
{
    public class ProbedResult
    {
        public ManifestV2List ListManifest { get; set; }
        public Manifest[] ManifestItems { get; set; }

        public long GetTotalSize()
        {
            if (ManifestItems == null || ManifestItems.Length == 0)
            {
                return 0;
            }
            
            return ManifestItems
                .Select(item => item.LayerTotalSize())
                .Sum();
        }

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