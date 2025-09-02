using System;
using System.Net.Sockets;

namespace DnsClientX {
    /// <summary>
    /// Describes a DNS resolver endpoint and its behavior.
    /// </summary>
    public sealed class DnsResolverEndpoint {
        /// <summary>
        /// Hostname or IP address for UDP/TCP/DoT. For DoH, the host part of <see cref="DohUrl"/>.
        /// </summary>
        public string? Host { get; init; }

        /// <summary>
        /// Port number. Defaults to 53 for UDP/TCP, 853 for DoT, and 443 for DoH when not specified.
        /// </summary>
        public int Port { get; init; } = 53;

        /// <summary>
        /// Optional preferred address family when resolving hostnames.
        /// </summary>
        public AddressFamily? Family { get; init; }

        /// <summary>
        /// Transport protocol used to query this endpoint.
        /// </summary>
        public Transport Transport { get; init; } = Transport.Udp;

        /// <summary>
        /// Optional per-endpoint timeout. When null, a library default is used.
        /// </summary>
        public TimeSpan? Timeout { get; init; }

        /// <summary>
        /// Allow automatic TCP fallback when UDP responses are truncated.
        /// </summary>
        public bool AllowTcpFallback { get; init; } = true;

        /// <summary>
        /// Optional EDNS buffer size for UDP queries.
        /// </summary>
        public int? EdnsBufferSize { get; init; }

        /// <summary>
        /// Sets the DO bit (DNSSEC OK) in queries to request DNSSEC records.
        /// </summary>
        public bool? DnsSecOk { get; init; }

        /// <summary>
        /// For DoH transports, the full resolver URL (e.g. https://dns.google/dns-query).
        /// </summary>
        public Uri? DohUrl { get; init; }

        /// <summary>
        /// Returns a human-readable endpoint string.
        /// </summary>
        public override string ToString() {
            if (Transport == Transport.Doh && DohUrl != null) return DohUrl.ToString();
            var h = Host;
            string safe = "(unknown)";
            if (!string.IsNullOrWhiteSpace(h)) {
                safe = h!; // safe due to IsNullOrWhiteSpace check
            }
            return Port > 0 ? $"{safe}:{Port}" : safe;
        }
    }
}
