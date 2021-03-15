using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
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

        public string ToJsonString()
        {
            var format = @"{{""listManifest"":{0},""manifestItems"":[{1}]}}";

            var listManifestJson = ListManifest == null ? "null" : Encoding.UTF8.GetString(ListManifest.RawJsonBytes);
            var subItemsJson = string.Empty;

            if (ManifestItems != null && ManifestItems.Length > 0)
            {
                subItemsJson = string.Join(',', ManifestItems.Select(item => Encoding.UTF8.GetString(ListManifest.RawJsonBytes)));
            }
            return string.Format(format, listManifestJson, subItemsJson);
        }
    }

    public static class ManifestExtensions
    {
        public static long LayerTotalSize(this Manifest manifest)
        {
            return manifest.Layers?.Select(l => l.Size ?? 0).Sum() ?? 0;
        }
    }
}