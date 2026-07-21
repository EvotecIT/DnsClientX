using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Represents the resolver configuration discovered from the operating system.
    /// </summary>
    public sealed class SystemDnsConfiguration {
        private readonly ReadOnlyCollection<string> dnsServers;
        private readonly ReadOnlyCollection<string> searchDomains;

        internal SystemDnsConfiguration(
            IEnumerable<string>? dnsServers,
            IEnumerable<string>? searchDomains,
            int ndots,
            SystemDnsDiscoverySource source,
            string? error = null) {
            this.dnsServers = Array.AsReadOnly((dnsServers ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
            this.searchDomains = Array.AsReadOnly((searchDomains ?? Array.Empty<string>())
                .Select(NormalizeDomain)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
            Ndots = Math.Max(0, Math.Min(15, ndots));
            Source = source;
            Error = error;
        }

        /// <summary>Gets the DNS servers in operating-system preference order.</summary>
        public IReadOnlyList<string> DnsServers => dnsServers;

        /// <summary>Gets the configured DNS search suffixes in preference order.</summary>
        public IReadOnlyList<string> SearchDomains => searchDomains;

        /// <summary>
        /// Gets the number of dots that causes an unqualified name to be attempted as absolute before search suffixes.
        /// </summary>
        public int Ndots { get; }

        /// <summary>Gets the source that supplied this configuration.</summary>
        public SystemDnsDiscoverySource Source { get; }

        /// <summary>Gets a discovery error when the operating-system configuration could not be read.</summary>
        public string? Error { get; }

        /// <summary>Gets whether at least one DNS server was discovered.</summary>
        public bool HasDnsServers => dnsServers.Count > 0;

        /// <summary>
        /// Builds DNS query candidates using resolv.conf-style search and <c>ndots</c> ordering.
        /// </summary>
        /// <param name="name">The name supplied by the caller.</param>
        /// <returns>Candidate names in the order in which they should be queried.</returns>
        public IReadOnlyList<string> BuildQueryCandidates(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentNullException(nameof(name));
            }

            string trimmed = name.Trim();
            if (trimmed.EndsWith(".", StringComparison.Ordinal) || searchDomains.Count == 0) {
                return Array.AsReadOnly(new[] { trimmed });
            }

            int dotCount = trimmed.Count(character => character == '.');
            var candidates = new List<string>(searchDomains.Count + 1);
            if (dotCount >= Ndots) {
                candidates.Add(trimmed);
            }

            foreach (string suffix in searchDomains) {
                candidates.Add($"{trimmed}.{suffix}");
            }

            if (dotCount < Ndots) {
                candidates.Add(trimmed);
            }

            return Array.AsReadOnly(candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        private static string NormalizeDomain(string value) {
            return (value ?? string.Empty).Trim().Trim('.');
        }
    }
}
