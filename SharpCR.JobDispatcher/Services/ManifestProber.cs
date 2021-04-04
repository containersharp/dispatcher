using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Authentication;
using Docker.Registry.DotNet.Registry;
using Microsoft.Extensions.Logging;
using SharpCR.JobDispatcher.Models;
using SharpCR.Manifests;
using Manifest = SharpCR.Manifests.Manifest;

namespace SharpCR.JobDispatcher.Services
{
    public class ManifestProber
    {
        private readonly ILogger<ManifestProber> _logger;
        private readonly IManifestParser[] _parsers;
        public ManifestProber(ILogger<ManifestProber> logger)
        {
            _logger = logger;
            var parserType = typeof(IManifestParser);
            _parsers = parserType.Assembly.GetExportedTypes()
                .Where(t => (t.IsPublic || t.IsNestedPublic) && t.IsClass && parserType.IsAssignableFrom(t))
                .Select(x => Activator.CreateInstance(x) as IManifestParser)
                .ToArray();
        }

        public async Task<ProbedResult> ProbeManifestAsync(Job jobRequest, bool digSubManifests)
        {
            var jobPublic = jobRequest.ToPublicModel();
            var reference = string.IsNullOrEmpty(jobRequest.Tag) ? jobRequest.Digest : jobRequest.Tag;
            var probeContext = GetProbeContext(jobRequest);

            try
            {
                var configuration = new RegistryClientConfiguration(probeContext.Registry);
                var credentials = string.IsNullOrEmpty(jobRequest.AuthorizationToken)
                    ? null
                    : jobRequest.AuthorizationToken.Split(':', StringSplitOptions.RemoveEmptyEntries);

                var authProvider = credentials == null
                    ? (AuthenticationProvider)new AnonymousOAuthAuthenticationProvider()
                    : new PasswordOAuthAuthenticationProvider(credentials.Length > 1 ? credentials[0] : "token",  credentials[1]);
                using var client = configuration.CreateClient(authProvider);

                var manifestResult = await client.Manifest.GetManifestAsync(probeContext.RepoName, reference);
                var parsedManifest = TryParseManifestFromResponse(Encoding.UTF8.GetBytes(manifestResult.Content));

                if (parsedManifest == null)
                {
                    return null;
                }

                return await WrapAsResult(parsedManifest, client, probeContext.RepoName, digSubManifests);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error probing manifest for job {@job}. Error: {@ex}", jobPublic, ex.ToString());
                return null;
            }
        }

        private async Task<ProbedResult> WrapAsResult(Manifest parsedManifest, IRegistryClient client, string repoName, bool digSubManifests)
        {
            var probedResult = new ProbedResult();
            
            if (parsedManifest is ManifestV2List manifestV2List)
            {
                probedResult.ListManifest = manifestV2List;
                if (digSubManifests)
                {
                    var subManifests = new List<Manifest>();
                    foreach (var listItem in manifestV2List.Manifests)
                    {
                        var subManifestResult = await client.Manifest.GetManifestAsync(repoName, listItem.Digest);
                        var manifest = TryParseManifestFromResponse(Encoding.UTF8.GetBytes(subManifestResult.Content));
                        if (manifest != null)
                        {
                            subManifests.Add(manifest);
                        }
                    }

                    probedResult.ManifestItems = subManifests.ToArray();
                }
            }
            else
            {
                probedResult.ManifestItems = new[] {parsedManifest};
            }

            return probedResult;
        }

        static ProbeContext GetProbeContext(Job job)
        {
            var rawRepoName = job.ImageRepository;
            var fakeUri = new Uri($"https://{rawRepoName}");
            if (!WellKnownRegistryMapping.TryGetValue(fakeUri.Host, out var registryHost))
            {
                registryHost = fakeUri.GetComponents(UriComponents.Host | UriComponents.Port, UriFormat.SafeUnescaped);
            }

            return new ProbeContext
            {
                Registry = registryHost,
                RepoName = fakeUri.AbsolutePath.Substring(1),
                DigestOrTag = job.Digest ?? job.Tag
            };
        }

        private static readonly Dictionary<string, string> WellKnownRegistryMapping = new Dictionary<string, string>()
        {
            {"docker.io", "registry-1.docker.io"},
            {"index.docker.io", "registry-1.docker.io"},
            {"hub.docker.com", "registry-1.docker.io"},
        };

        Manifest TryParseManifestFromResponse(byte[] bytes)
        {
            return _parsers.Select(p =>
                {
                    try
                    {
                        return p.Parse(bytes);
                    }
                    catch { return null; }
                })
                .FirstOrDefault(m => m != null);
        }
        
        class ProbeContext
        {
            public string Registry { get; set; } 
            public string RepoName { get; set; }
            public string DigestOrTag { get; set; }
        }
    }
}