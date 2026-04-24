using System;

namespace DnsClientX {
    /// <summary>
    /// Describes a parsed DNS stamp in a user-facing shape.
    /// </summary>
    public sealed class DnsStampInfo {
        /// <summary>
        /// Original DNS stamp input.
        /// </summary>
        public string Stamp { get; init; } = string.Empty;

        /// <summary>
        /// Normalized DNS stamp generated from the parsed endpoint.
        /// </summary>
        public string NormalizedStamp { get; init; } = string.Empty;

        /// <summary>
        /// Parsed resolver endpoint.
        /// </summary>
        public DnsResolverEndpoint Endpoint { get; init; } = new DnsResolverEndpoint();

        /// <summary>
        /// Transport represented by the stamp.
        /// </summary>
        public Transport Transport { get; init; }

        /// <summary>
        /// Request format represented by the stamp.
        /// </summary>
        public DnsRequestFormat? RequestFormat { get; init; }

        /// <summary>
        /// Hostname or IP address represented by the stamp.
        /// </summary>
        public string? Host { get; init; }

        /// <summary>
        /// Port represented by the stamp.
        /// </summary>
        public int Port { get; init; }

        /// <summary>
        /// DNS-over-HTTPS URI when the stamp represents DoH.
        /// </summary>
        public Uri? DohUrl { get; init; }

        /// <summary>
        /// Indicates whether the DNSSEC property is set in the stamp.
        /// </summary>
        public bool DnsSecOk { get; init; }
    }
}
