using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Clears the multi-resolver fastest-endpoint cache used by the FastestWins strategy.</para>
    /// <para type="description">Clears the in-memory cache used to remember the fastest endpoint for a given set of endpoints. Use this when you change network conditions or want to force re-probing. Does not affect TTL-based response caching, which expires automatically.</para>
    /// <example>
    /// <code>Clear-DnsMultiResolverCache</code>
    /// <para>Clears the entire FastestWins cache.</para>
    /// </example>
    /// <example>
    /// <code>Clear-DnsMultiResolverCache -ResolverDnsProvider Cloudflare,Google</code>
    /// <para>Clears cache entries only for the specified provider set.</para>
    /// </example>
    /// <example>
    /// <code>Clear-DnsMultiResolverCache -ResolverEndpoint '1.1.1.1:53','https://dns.google/dns-query'</code>
    /// <para>Clears cache entries only for the specified endpoints.</para>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Clear, "DnsMultiResolverCache", DefaultParameterSetName = "All")]
    public sealed class CmdletClearDnsMultiResolverCache : PSCmdlet {
        /// <summary>
        /// <para type="description">Clear all cached fastest-endpoint choices.</para>
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "All")]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// <para type="description">Specific endpoints to clear, e.g. "1.1.1.1:53", "[2606:4700:4700::1111]:53", or DoH URLs.</para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// <para type="description">Clear cache entries for the specified predefined providers (DnsEndpoint enum).</para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverDnsProvider")]
        public DnsEndpoint[] ResolverDnsProvider { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// <para type="description">Alternate parameter: providers to clear when using the classic -DnsProvider parameter style.</para>
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "DnsProvider")]
        public DnsEndpoint[] DnsProvider { get; set; } = Array.Empty<DnsEndpoint>();

        /// <inheritdoc />
        protected override void ProcessRecord() {
            if (this.ParameterSetName == "All" || (ResolverEndpoint.Length == 0 && ResolverDnsProvider.Length == 0 && DnsProvider.Length == 0)) {
                DnsMultiResolver.ClearFastestCache();
                WriteVerbose("Cleared entire FastestWins cache.");
                return;
            }

            List<DnsResolverEndpoint> endpoints = new();
            if (this.ParameterSetName == "ResolverEndpoint") {
                var parsed = EndpointParser.TryParseMany(ResolverEndpoint, out var errors);
                foreach (var e in errors) { WriteWarning(e); }
                if (parsed.Length == 0) return;
                endpoints.AddRange(parsed);
            } else {
                var providers = this.ParameterSetName == "ResolverDnsProvider" ? ResolverDnsProvider : DnsProvider;
                foreach (var ep in providers) {
                    endpoints.AddRange(DnsResolverEndpointFactory.From(ep));
                }
            }

            if (endpoints.Count > 0) {
                DnsMultiResolver.ClearFastestCacheFor(endpoints);
                WriteVerbose($"Cleared FastestWins cache for {endpoints.Count} endpoint(s).");
            }
        }
    }
}
