using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpCR.JobDispatcher.Models;
using SharpCR.Manifests;

namespace SharpCR.JobDispatcher.Services
{
    public class ManifestProber
    {
        private readonly ILogger<ManifestProber> _logger;
        private readonly HttpClient _httpClient;
        private IManifestParser[] _parsers;
        public ManifestProber(ILogger<ManifestProber> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Docker-Client/19.03.5 (linux)");

            var parserType = typeof(IManifestParser);
            _parsers = parserType.Assembly.GetExportedTypes()
                .Where(t => t.IsPublic && t.IsClass && parserType.IsAssignableFrom(t))
                .Select(x => Activator.CreateInstance(x) as IManifestParser)
                .ToArray();
        }
        
        
        public async Task<ProbedManifest> ProbeManifestAsync(Job jobRequest)
        {
            var jobPublic = jobRequest.ToPublicModel();
            var reference = string.IsNullOrEmpty(jobRequest.Tag) ? jobRequest.Digest : jobRequest.Tag;
            var manifestUrl = $"https://{jobRequest.ImageRepository}/v2/manifests/{reference}";
            
            var probeRequest = new HttpRequestMessage(HttpMethod.Get, manifestUrl);
            if (!string.IsNullOrEmpty(jobRequest.AuthorizationToken))
            {
                probeRequest.Headers.TryAddWithoutValidation("Authorization", jobRequest.AuthorizationToken);
            }
            else
            {
                // todo: add default auth info for different repositories
            }

            try
            {
                var manifestResponse = await _httpClient.SendAsync(probeRequest);
                if (!manifestResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed http response @code for manifest probing for @job.", manifestResponse.StatusCode, jobPublic);
                    return null;
                }

                var manifest = await TryParseManifestFromResponse(manifestResponse);
                if (manifest == null)
                {
                    _logger.LogWarning("Error parsing manifest for job @job.", jobPublic);
                    return null;
                }

                return new ProbedManifest
                {
                    Bytes = manifest.RawJsonBytes,
                    MediaType = manifest.MediaType ?? manifestResponse.Content.Headers?.ContentType?.MediaType,
                    Size = (manifest.Layers ?? new Descriptor[0]).Select(l => l.Size ?? 0).Sum()
                };
            }
            catch(Exception ex)
            {
                _logger.LogWarning("Error probing manifest for job @job. Error: @ex", jobPublic, ex.ToString());
                return null;
            }
        }

        async Task<Manifest> TryParseManifestFromResponse(HttpResponseMessage response)
        {
            if (response.Content == null)
            {
                return null;
            }

            var content =  await response.Content.ReadAsByteArrayAsync();
            return _parsers.Select(p =>
                {
                    try
                    {
                        return p.Parse(content);
                    }
                    catch { return null; }
                })
                .FirstOrDefault(m => m != null);
        }
    }
}