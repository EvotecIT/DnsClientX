using System;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Describes one resolver target source that can be expanded into runnable execution targets.
    /// </summary>
    public sealed class ResolverExecutionTargetSource {
        /// <summary>
        /// Gets or sets built-in resolver endpoints that should be queried directly.
        /// </summary>
        public DnsEndpoint[] BuiltInEndpoints { get; set; } = Array.Empty<DnsEndpoint>();

        /// <summary>
        /// Gets or sets a built-in resolver profile that should be expanded for probing.
        /// </summary>
        public DnsEndpoint? ProbeProfile { get; set; }

        /// <summary>
        /// Gets or sets inline explicit resolver endpoint values.
        /// </summary>
        /// <remarks>
        /// Examples include <c>udp@1.1.1.1:53</c>, <c>doq@dns.quad9.net:853</c>, and
        /// <c>doh3@https://dns.quad9.net/dns-query</c>.
        /// </remarks>
        public string[] ResolverEndpoints { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets file paths that contain explicit resolver endpoint values.
        /// </summary>
        public string[] ResolverEndpointFiles { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets URLs that expose explicit resolver endpoint values.
        /// </summary>
        public string[] ResolverEndpointUrls { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets an optional persisted resolver selection snapshot path.
        /// </summary>
        public string? ResolverSelectionPath { get; set; }

        /// <summary>
        /// Gets a value indicating whether inline, file, or URL explicit endpoint inputs were provided.
        /// </summary>
        public bool HasExplicitResolverInputs =>
            ResolverEndpoints.Length > 0 ||
            ResolverEndpointFiles.Length > 0 ||
            ResolverEndpointUrls.Length > 0;

        internal void Validate() {
            int configuredSources = 0;
            if (BuiltInEndpoints.Length > 0) {
                configuredSources++;
            }

            if (ProbeProfile.HasValue) {
                configuredSources++;
            }

            if (HasExplicitResolverInputs) {
                configuredSources++;
            }

            if (!string.IsNullOrWhiteSpace(ResolverSelectionPath)) {
                configuredSources++;
            }

            if (configuredSources != 1) {
                throw new InvalidOperationException("Specify exactly one resolver target source: BuiltInEndpoints, ProbeProfile, ResolverEndpoints/ResolverEndpointFiles/ResolverEndpointUrls, or ResolverSelectionPath.");
            }

            if (BuiltInEndpoints.Any(endpoint => endpoint == DnsEndpoint.Custom)) {
                throw new InvalidOperationException("BuiltInEndpoints cannot contain DnsEndpoint.Custom.");
            }
        }
    }
}
