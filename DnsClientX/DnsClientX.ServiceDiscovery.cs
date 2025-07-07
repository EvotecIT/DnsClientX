using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class containing service discovery helpers.
    /// </summary>
    public partial class ClientX {
        internal Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>>? ResolverOverride;

        /// <summary>
        /// Resolves a DNS query specifically for service discovery, allowing tests to override the resolver.
        /// </summary>
        /// <param name="name">The fully qualified domain name to query.</param>
        /// <param name="type">The DNS record type.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        private Task<DnsResponse> ResolveForSd(string name, DnsRecordType type, CancellationToken cancellationToken) {
            if (ResolverOverride != null) {
                return ResolverOverride(name, type, cancellationToken);
            }

            return Resolve(name, type, requestDnsSec: false, validateDnsSec: false, returnAllTypes: false, retryOnTransient: true, maxRetries: 3, retryDelayMs: 100, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Discovers DNS-SD services under the specified domain.
        /// </summary>
        /// <param name="domain">Domain name to look up for advertised services.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>An array of discovered services or an empty array if none found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> is null or whitespace.</exception>
        public async Task<DnsServiceDiscovery[]> DiscoverServices(string domain, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentNullException(nameof(domain));
            string ptrQuery = $"_services._dns-sd._udp.{domain}";
            var ptrResponse = await ResolveForSd(ptrQuery, DnsRecordType.PTR, cancellationToken).ConfigureAwait(false);
            if (ptrResponse.Answers == null) return Array.Empty<DnsServiceDiscovery>();

            var results = new List<DnsServiceDiscovery>();
            foreach (var ptr in ptrResponse.Answers.Where(a => a.Type == DnsRecordType.PTR)) {
                string serviceDomain = ptr.Data.TrimEnd('.');
                var srvResponse = await ResolveForSd(serviceDomain, DnsRecordType.SRV, cancellationToken).ConfigureAwait(false);
                var txtResponse = await ResolveForSd(serviceDomain, DnsRecordType.TXT, cancellationToken).ConfigureAwait(false);

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var txt in txtResponse.Answers?.Where(a => a.Type == DnsRecordType.TXT) ?? Array.Empty<DnsAnswer>()) {
                    foreach (string part in txt.DataStringsEscaped) {
                        var idx = part.IndexOf('=');
                        if (idx > 0) {
                            string key = part.Substring(0, idx);
                            string val = part.Substring(idx + 1);
                            metadata[key] = val;
                        } else {
                            metadata[part] = string.Empty;
                        }
                    }
                }

                foreach (var srv in srvResponse.Answers?.Where(a => a.Type == DnsRecordType.SRV) ?? Array.Empty<DnsAnswer>()) {
                    var bits = srv.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length == 4 &&
                        int.TryParse(bits[0], out int priority) &&
                        int.TryParse(bits[1], out int weight) &&
                        int.TryParse(bits[2], out int port)) {
                        string target = bits[3].TrimEnd('.');
                        results.Add(new DnsServiceDiscovery {
                            ServiceName = serviceDomain,
                            Target = target,
                            Port = port,
                            Priority = priority,
                            Weight = weight,
                            Metadata = metadata.Count > 0 ? new Dictionary<string, string>(metadata) : null
                        });
                    }
                }
            }

            return results.ToArray();
        }

        /// <summary>
        /// Streams DNS-SD services discovered under the specified domain.
        /// </summary>
        /// <param name="domain">Domain name to look up for advertised services.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>An asynchronous enumeration of discovered services.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="domain"/> is null or whitespace.</exception>
        public async IAsyncEnumerable<DnsServiceDiscovery> EnumerateServicesAsync(
            string domain,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentNullException(nameof(domain));
            string ptrQuery = $"_services._dns-sd._udp.{domain}";
            var ptrResponse = await ResolveForSd(ptrQuery, DnsRecordType.PTR, cancellationToken).ConfigureAwait(false);
            if (ptrResponse.Answers == null) yield break;

            foreach (var ptr in ptrResponse.Answers.Where(a => a.Type == DnsRecordType.PTR)) {
                string serviceDomain = ptr.Data.TrimEnd('.');
                var srvResponse = await ResolveForSd(serviceDomain, DnsRecordType.SRV, cancellationToken).ConfigureAwait(false);
                var txtResponse = await ResolveForSd(serviceDomain, DnsRecordType.TXT, cancellationToken).ConfigureAwait(false);

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var txt in txtResponse.Answers?.Where(a => a.Type == DnsRecordType.TXT) ?? Array.Empty<DnsAnswer>()) {
                    foreach (string part in txt.DataStringsEscaped) {
                        var idx = part.IndexOf('=');
                        if (idx > 0) {
                            string key = part.Substring(0, idx);
                            string val = part.Substring(idx + 1);
                            metadata[key] = val;
                        } else {
                            metadata[part] = string.Empty;
                        }
                    }
                }

                foreach (var srv in srvResponse.Answers?.Where(a => a.Type == DnsRecordType.SRV) ?? Array.Empty<DnsAnswer>()) {
                    var bits = srv.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (bits.Length == 4 &&
                        int.TryParse(bits[0], out int priority) &&
                        int.TryParse(bits[1], out int weight) &&
                        int.TryParse(bits[2], out int port)) {
                        string target = bits[3].TrimEnd('.');
                        yield return new DnsServiceDiscovery {
                            ServiceName = serviceDomain,
                            Target = target,
                            Port = port,
                            Priority = priority,
                            Weight = weight,
                            Metadata = metadata.Count > 0 ? new Dictionary<string, string>(metadata) : null
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Resolves SRV records for a specific service and protocol under a domain.
        /// </summary>
        /// <param name="service">Service name without leading underscore, e.g. <c>ldap</c>.</param>
        /// <param name="protocol">Protocol name without leading underscore, e.g. <c>tcp</c>.</param>
        /// <param name="domain">Domain hosting the service.</param>
        /// <param name="resolveHosts">Whether to resolve A and AAAA records for each target.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Ordered SRV records parsed from the response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null or whitespace.</exception>
        public async Task<DnsSrvRecord[]> ResolveServiceAsync(
            string service,
            string protocol,
            string domain,
            bool resolveHosts = false,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(service)) throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrWhiteSpace(protocol)) throw new ArgumentNullException(nameof(protocol));
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentNullException(nameof(domain));

            if (!service.StartsWith("_", StringComparison.Ordinal)) service = "_" + service;
            if (!protocol.StartsWith("_", StringComparison.Ordinal)) protocol = "_" + protocol;

            string query = $"{service}.{protocol}.{domain}";
            var response = await ResolveForSd(query, DnsRecordType.SRV, cancellationToken).ConfigureAwait(false);
            if (response.Answers == null) return Array.Empty<DnsSrvRecord>();

            var records = new List<DnsSrvRecord>();
            foreach (var answer in response.Answers.Where(a => a.Type == DnsRecordType.SRV)) {
                var parts = answer.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4 &&
                    int.TryParse(parts[0], out int priority) &&
                    int.TryParse(parts[1], out int weight) &&
                    int.TryParse(parts[2], out int port)) {
                    string target = parts[3].TrimEnd('.');
                    IPAddress[]? addresses = null;
                    if (resolveHosts) {
                        var addr = new List<IPAddress>();
                        var aRes = await ResolveForSd(target, DnsRecordType.A, cancellationToken).ConfigureAwait(false);
                        if (aRes.Answers != null) {
                            addr.AddRange(aRes.Answers.Where(a => a.Type == DnsRecordType.A)
                                .Select(a => IPAddress.Parse(a.Data)));
                        }
                        var aaaaRes = await ResolveForSd(target, DnsRecordType.AAAA, cancellationToken).ConfigureAwait(false);
                        if (aaaaRes.Answers != null) {
                            addr.AddRange(aaaaRes.Answers.Where(a => a.Type == DnsRecordType.AAAA)
                                .Select(a => IPAddress.Parse(a.Data)));
                        }
                        if (addr.Count > 0) addresses = addr.ToArray();
                    }

                    records.Add(new DnsSrvRecord {
                        Target = target,
                        Port = port,
                        Priority = priority,
                        Weight = weight,
                        Addresses = addresses
                    });
                }
            }

            return records
                .OrderBy(r => r.Priority)
                .ThenByDescending(r => r.Weight)
                .ToArray();
        }
    }
}
