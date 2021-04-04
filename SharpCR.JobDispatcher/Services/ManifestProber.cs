using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Docker.Registry.DotNet;
using Docker.Registry.DotNet.Authentication;
using Docker.Registry.DotNet.Registry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCR.JobDispatcher.Models;
using SharpCR.Manifests;
using Manifest = SharpCR.Manifests.Manifest;

namespace SharpCR.JobDispatcher.Services
{
    public class ManifestProber
    {
        private readonly ILogger<ManifestProber> _logger;
        private readonly DispatcherConfig _config;
        private readonly IManifestParser[] _parsers;
        public ManifestProber(ILogger<ManifestProber> logger, IOptions<DispatcherConfig> dispatcherOptions)
        {
            _logger = logger;
            _config = dispatcherOptions.Value;
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
            var probeContext = JobProcessor.ParseAsInternalContext(jobRequest);

            try
            {
                var configuration = new RegistryClientConfiguration(probeContext.Registry);
                var authProvider = CreateAuthProvider(jobRequest, probeContext);
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

        private AuthenticationProvider CreateAuthProvider(Job jobRequest, JobProcessor.ProbeContext probeContext)
        {
            var credential = JobProcessor.GetRegistryCredential(ref jobRequest, _config.BuiltinCredentials);

            return credential == null
                ? (AuthenticationProvider) new AnonymousOAuthAuthenticationProvider()
                : new PasswordOAuthAuthenticationProvider(credential.Username, credential.Password);
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