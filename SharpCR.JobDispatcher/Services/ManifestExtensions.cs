using System.Linq;
using SharpCR.Manifests;

namespace SharpCR.JobDispatcher.Services
{
    public static class ManifestExtensions
    {
        public static long LayerTotalSize(this Manifest manifest)
        {
            return manifest.Layers?.Select(l => l.Size ?? 0).Sum() ?? 0;
        }
    }
}