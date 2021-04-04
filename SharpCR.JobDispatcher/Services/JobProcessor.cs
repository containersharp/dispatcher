using System;
using System.Collections.Generic;
using System.Linq;
using SharpCR.JobDispatcher.Models;

namespace SharpCR.JobDispatcher.Services
{
    internal static class JobProcessor
    {
        private static readonly Dictionary<string, string> WellKnownRegistryMapping = new Dictionary<string, string>()
        {
            {"docker.io", "registry-1.docker.io"},
            {"index.docker.io", "registry-1.docker.io"},
            {"hub.docker.com", "registry-1.docker.io"},
        };

        public static ProbeContext ParseAsInternalContext(Job job)
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

        public static RegistryCredential GetRegistryCredential(ref Job job, RegistryCredential[] builtinCredentials)
        {
            AssignBuiltinCredentialIfRequired(ref job, builtinCredentials);
            var parts = job.AuthorizationToken.Split(':', StringSplitOptions.RemoveEmptyEntries);
            return new RegistryCredential
            {
                Username = parts.Length > 1 ? parts[0] : "token",
                Password = parts[1]
            };
        }
        
        public static void AssignBuiltinCredentialIfRequired(ref Job syncJob, RegistryCredential[] builtinCredentials)
        {
            if (!string.IsNullOrEmpty(syncJob.AuthorizationToken))
            {
                return;
            }

            var ctx = ParseAsInternalContext(syncJob);
            var builtinCredential = builtinCredentials.FirstOrDefault(c =>
                string.Equals(c.Hostname, ctx.Registry, StringComparison.OrdinalIgnoreCase));
            
            if (builtinCredential != null)
            {
                syncJob.AuthorizationToken = $"{builtinCredential.Username}:{builtinCredential.Password}";
            }
        }

        internal class ProbeContext
        {
            public string Registry { get; set; } 
            public string RepoName { get; set; }
            public string DigestOrTag { get; set; }
        }
    }
}