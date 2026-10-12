using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DnsClientX {
    /// <summary>Records environment and execution settings relevant to comparing resolver observations.</summary>
    public sealed class ResolverScoreRunContext {
        /// <summary>Gets or sets the host that performed the run.</summary>
        public string Host { get; set; } = string.Empty;
        /// <summary>Gets or sets the OS description.</summary>
        public string OperatingSystem { get; set; } = string.Empty;
        /// <summary>Gets or sets the executing framework description.</summary>
        public string Runtime { get; set; } = string.Empty;
        /// <summary>Gets or sets the process architecture.</summary>
        public string Architecture { get; set; } = string.Empty;
        /// <summary>Gets or sets whether DNSSEC records were requested.</summary>
        public bool RequestDnsSec { get; set; }
        /// <summary>Gets or sets whether local DNSSEC validation was requested.</summary>
        public bool ValidateDnsSec { get; set; }
        /// <summary>Gets or sets the canonical bootstrap execution key, when configured.</summary>
        public string? BootstrapEndpoint { get; set; }
        /// <summary>Gets or sets opaque canonical execution keys indexed by candidate label. Missing keys prevent scope comparison.</summary>
        public Dictionary<string, string>? TargetExecutionKeys { get; set; }
        /// <summary>Gets or sets explicit endpoint definitions for uniquely identified labels. Missing or ambiguous definitions cannot be reused automatically.</summary>
        public Dictionary<string, DnsResolverEndpoint>? ExplicitEndpoints { get; set; }
        /// <summary>Gets or sets whether DoH wire POST was forced.</summary>
        public bool ForceDohWirePost { get; set; }
        /// <summary>Gets or sets whether NSID was requested.</summary>
        public bool RequestNsid { get; set; }
        /// <summary>Gets or sets the retry count.</summary>
        public int MaxRetries { get; set; }
        /// <summary>Gets or sets the retry delay in milliseconds.</summary>
        public int RetryDelayMs { get; set; }
        /// <summary>Gets or sets the global port override.</summary>
        public int? PortOverride { get; set; }

        internal static ResolverScoreRunContext Capture(ResolverQueryRunOptions options, IReadOnlyList<ResolverExecutionTarget> targets) {
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var endpoints = new Dictionary<string, DnsResolverEndpoint>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in targets.GroupBy(target => target.DisplayName, StringComparer.OrdinalIgnoreCase)) {
                string[] identities = group.Select(target => target.ExplicitEndpoint != null
                    ? ResolverEndpointClientFactory.GetExecutionKey(target.ExplicitEndpoint)
                    : target.BuiltInEndpoint.HasValue ? "builtin:" + target.BuiltInEndpoint.Value : string.Empty)
                    .Distinct(StringComparer.Ordinal).ToArray();
                // A short display label can denote several distinct configurations. Preserve observations,
                // but do not pretend that the label identifies an endpoint for comparison or reuse.
                if (identities.Length != 1 || string.IsNullOrWhiteSpace(identities[0])) continue;
                keys.Add(group.Key, identities[0]);
                DnsResolverEndpoint? endpoint = group.First().ExplicitEndpoint;
                if (endpoint != null) endpoints.Add(group.Key, endpoint);
            }
            return new ResolverScoreRunContext {
                Host = Environment.MachineName, OperatingSystem = RuntimeInformation.OSDescription,
                Runtime = RuntimeInformation.FrameworkDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                RequestDnsSec = options.RequestDnsSec, ValidateDnsSec = options.ValidateDnsSec,
                BootstrapEndpoint = options.BootstrapResolver == null ? null : ResolverEndpointClientFactory.GetExecutionKey(options.BootstrapResolver),
                TargetExecutionKeys = keys, ExplicitEndpoints = endpoints,
                ForceDohWirePost = options.ForceDohWirePost, RequestNsid = options.RequestNsid,
                MaxRetries = options.MaxRetries, RetryDelayMs = options.RetryDelayMs, PortOverride = options.PortOverride
            };
        }

        internal bool TryGetExecutionKey(string target, out string? key) {
            key = null;
            if (TargetExecutionKeys == null) return false;
            var matches = TargetExecutionKeys.Where(pair => string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].Value)) return false;
            key = matches[0].Value;
            return true;
        }

        internal bool TryGetExplicitEndpoint(string target, out DnsResolverEndpoint? endpoint) {
            endpoint = null;
            if (ExplicitEndpoints == null) return false;
            var matches = ExplicitEndpoints.Where(pair => string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1 || matches[0].Value == null) return false;
            endpoint = matches[0].Value;
            return true;
        }
    }
}
