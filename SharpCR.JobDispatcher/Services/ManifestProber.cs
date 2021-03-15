﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Authentication;
using Docker.Registry.DotNet.Models;
using Microsoft.Extensions.Logging;
using SharpCR.JobDispatcher.Models;
using SharpCR.Manifests;
using Manifest = SharpCR.Manifests.Manifest;

namespace SharpCR.JobDispatcher.Services
{
    public class ManifestProber
    {
        private const string ManifestUrlPattern = "/v2/manifests/";
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
                .Where(t => (t.IsPublic || t.IsNestedPublic) && t.IsClass && parserType.IsAssignableFrom(t))
                .Select(x => Activator.CreateInstance(x) as IManifestParser)
                .ToArray();
        }

        public async Task<ProbedResult> ProbeManifestAsync(Job jobRequest)
        {
            var jobPublic = jobRequest.ToPublicModel();
            var reference = string.IsNullOrEmpty(jobRequest.Tag) ? jobRequest.Digest : jobRequest.Tag;
            var manifestUrl = GetRegistryManifestUri(jobPublic.ImageRepository);

            try
            {
                var configuration = new RegistryClientConfiguration(manifestUrl.GetComponents(UriComponents.Host | UriComponents.Port, UriFormat.SafeUnescaped));
                var authProvider = new AnonymousOAuthAuthenticationProvider();
                using var client = configuration.CreateClient(authProvider);

                var repoName = manifestUrl.PathAndQuery.Substring(1, 
                    manifestUrl.PathAndQuery.IndexOf(ManifestUrlPattern, StringComparison.Ordinal) -1);
                
                var manifestResult = await client.Manifest.GetManifestAsync(repoName, reference);
                var parsedManifest = TryParseManifestFromResponse(Encoding.UTF8.GetBytes(manifestResult.Content));

                if (parsedManifest == null)
                {
                    return null;
                }

                var probeManifestAsync = new ProbedResult();
                if (parsedManifest is ManifestV2List manifestV2List)
                {
                    probeManifestAsync.ListManifest = manifestV2List;
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
                    probeManifestAsync.ManifestItems = subManifests.ToArray();
                }
                else
                {
                    probeManifestAsync.ManifestItems = new[] {parsedManifest};
                }
                return probeManifestAsync;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error probing manifest for job @job. Error: @ex", jobPublic, ex.ToString());
                return null;
            }
        }


        static Uri GetRegistryManifestUri(string imageRegistry)
        {
            var parts = imageRegistry.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                imageRegistry = $"docker.io/library/{imageRegistry}";
            }
            if (parts.Length == 2)
            {
                imageRegistry = $"docker.io/{imageRegistry}";
            }

            var fakeUri = new Uri($"https://{imageRegistry}{ManifestUrlPattern}");
            var manifestUriBuilder = new UriBuilder
            {
                Scheme = Uri.UriSchemeHttps,
                Port = fakeUri.IsDefaultPort ? -1 : fakeUri.Port,
                Path = $"{fakeUri.PathAndQuery}"
            };
            if (!WellKnownRegistryMapping.TryGetValue(fakeUri.Host, out var registryHost))
            {
                registryHost = fakeUri.Host;
            }
            manifestUriBuilder.Host = registryHost;
            return manifestUriBuilder.Uri;
        }

        private static readonly Dictionary<string, string> WellKnownRegistryMapping = new Dictionary<string, string>()
        {
            {"docker.io", "registry-1.docker.io"},
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
    }
}