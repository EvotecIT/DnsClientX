using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class containing RFC 6763 service-discovery helpers.
    /// </summary>
    public partial class ClientX {
        internal Func<string, DnsRecordType, CancellationToken, Task<DnsResponse>>? ResolverOverride;

        /// <summary>
        /// Discovers all service types and instances advertised under a DNS domain.
        /// </summary>
        /// <param name="domain">Domain name to browse.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The discovered service instances.</returns>
        public async Task<DnsService[]> DiscoverServices(string domain, CancellationToken cancellationToken = default) {
            var results = new List<DnsService>();
            await foreach (DnsService service in EnumerateServicesAsync(domain, cancellationToken).ConfigureAwait(false)) {
                results.Add(service);
            }

            return results.ToArray();
        }

        /// <summary>
        /// Discovers service types advertised through the RFC 6763 meta-query.
        /// </summary>
        /// <param name="domain">Domain name to browse.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Distinct fully qualified service types.</returns>
        public async Task<string[]> DiscoverServiceTypesAsync(
            string domain,
            CancellationToken cancellationToken = default) {
            string normalizedDomain = NormalizeDiscoveryDomain(domain, nameof(domain));
            DnsResponse response = await ResolveDiscoveryRecordAsync(
                $"_services._dns-sd._udp.{normalizedDomain}",
                DnsRecordType.PTR,
                cancellationToken).ConfigureAwait(false);

            return GetAnswers(response, DnsRecordType.PTR)
                .Select(GetPtrTarget)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        /// <summary>
        /// Streams all service instances advertised under a DNS domain.
        /// </summary>
        /// <param name="domain">Domain name to browse.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>Service instances after their SRV and TXT records have been resolved.</returns>
        public async IAsyncEnumerable<DnsService> EnumerateServicesAsync(
            string domain,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            string normalizedDomain = NormalizeDiscoveryDomain(domain, nameof(domain));
            string[] serviceTypes = await DiscoverServiceTypesAsync(normalizedDomain, cancellationToken).ConfigureAwait(false);
            foreach (string serviceType in serviceTypes) {
                await foreach (DnsService service in EnumerateServiceTypeAsync(serviceType, cancellationToken).ConfigureAwait(false)) {
                    yield return service;
                }
            }
        }

        /// <summary>
        /// Browses instances of one service and transport under a DNS domain.
        /// </summary>
        /// <param name="service">Service label with or without the leading underscore.</param>
        /// <param name="protocol">Transport label with or without the leading underscore.</param>
        /// <param name="domain">Domain name to browse.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>The discovered service instances.</returns>
        public async Task<DnsService[]> BrowseServicesAsync(
            string service,
            string protocol,
            string domain,
            CancellationToken cancellationToken = default) {
            string serviceType = BuildServiceType(service, protocol, domain);
            var results = new List<DnsService>();
            await foreach (DnsService item in EnumerateServiceTypeAsync(serviceType, cancellationToken).ConfigureAwait(false)) {
                results.Add(item);
            }

            return results.ToArray();
        }

        /// <summary>
        /// Resolves SRV records for a service and returns them in RFC 2782 connection-attempt order.
        /// </summary>
        /// <param name="service">Service label with or without the leading underscore.</param>
        /// <param name="protocol">Transport label with or without the leading underscore.</param>
        /// <param name="domain">Domain hosting the service.</param>
        /// <param name="resolveHosts">Whether to resolve A and AAAA records for each target.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        /// <returns>SRV records ordered by priority and weighted random selection.</returns>
        public async Task<DnsSrvRecord[]> ResolveServiceAsync(
            string service,
            string protocol,
            string domain,
            bool resolveHosts = false,
            CancellationToken cancellationToken = default) {
            string query = BuildServiceType(service, protocol, domain);
            DnsResponse response = await ResolveDiscoveryRecordAsync(query, DnsRecordType.SRV, cancellationToken).ConfigureAwait(false);
            var records = new List<DnsSrvRecord>();
            foreach (DnsAnswer answer in GetAnswers(response, DnsRecordType.SRV)) {
                if (!TryParseSrv(answer, out DnsSrvRecord? record) || record == null) {
                    continue;
                }

                if (resolveHosts && record.Target != ".") {
                    record.Addresses = await ResolveServiceAddressesAsync(record.Target, cancellationToken).ConfigureAwait(false);
                }

                records.Add(record);
            }

            return DnsServiceSelection.OrderForConnection(records);
        }

        private async IAsyncEnumerable<DnsService> EnumerateServiceTypeAsync(
            string serviceType,
            [EnumeratorCancellation] CancellationToken cancellationToken) {
            DnsResponse instanceResponse = await ResolveDiscoveryRecordAsync(
                serviceType,
                DnsRecordType.PTR,
                cancellationToken).ConfigureAwait(false);
            string[] instances = GetAnswers(instanceResponse, DnsRecordType.PTR)
                .Select(GetPtrTarget)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string instance in instances) {
                DnsResponse srvResponse = await ResolveDiscoveryRecordAsync(instance, DnsRecordType.SRV, cancellationToken).ConfigureAwait(false);
                DnsResponse txtResponse = await ResolveDiscoveryRecordAsync(instance, DnsRecordType.TXT, cancellationToken).ConfigureAwait(false);
                Dictionary<string, string>? metadata = ParseTxtMetadata(GetAnswers(txtResponse, DnsRecordType.TXT));

                foreach (DnsAnswer answer in GetAnswers(srvResponse, DnsRecordType.SRV)) {
                    if (TryParseSrv(answer, out DnsSrvRecord? record) && record != null) {
                        yield return new DnsService {
                            ServiceName = instance,
                            ServiceType = serviceType,
                            Target = record.Target,
                            Port = record.Port,
                            Priority = record.Priority,
                            Weight = record.Weight,
                            Metadata = metadata == null ? null : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)
                        };
                    }
                }
            }
        }

        private Task<DnsResponse> ResolveForSd(
            string name,
            DnsRecordType type,
            CancellationToken cancellationToken) {
            if (ResolverOverride != null) {
                return ResolverOverride(name, type, cancellationToken);
            }

            return Resolve(
                name,
                type,
                requestDnsSec: false,
                validateDnsSec: false,
                returnAllTypes: true,
                retryOnTransient: true,
                maxRetries: 3,
                retryDelayMs: 100,
                cancellationToken: cancellationToken);
        }

        private async Task<DnsResponse> ResolveDiscoveryRecordAsync(
            string name,
            DnsRecordType type,
            CancellationToken cancellationToken) {
            try {
                return await ResolveForSd(name, type, cancellationToken).ConfigureAwait(false);
            } catch (DnsClientException ex) when (ex.Response?.Status == DnsResponseCode.NXDomain
                || ex.Response?.Status == DnsResponseCode.NXRRSet) {
                return ex.Response;
            }
        }

        private async Task<IPAddress[]?> ResolveServiceAddressesAsync(
            string target,
            CancellationToken cancellationToken) {
            var addresses = new List<IPAddress>();
            foreach (DnsRecordType type in new[] { DnsRecordType.A, DnsRecordType.AAAA }) {
                DnsResponse response = await ResolveDiscoveryRecordAsync(target, type, cancellationToken).ConfigureAwait(false);
                foreach (DnsAnswer answer in GetAnswers(response, type)) {
                    if (IPAddress.TryParse(answer.Data, out IPAddress? address)) {
                        addresses.Add(address);
                    }
                }
            }

            return addresses.Count == 0 ? null : addresses.Distinct().ToArray();
        }

        private static IEnumerable<DnsAnswer> GetAnswers(DnsResponse response, DnsRecordType type) {
            return (response.Answers ?? Array.Empty<DnsAnswer>()).Where(answer => answer.Type == type);
        }

        private static Dictionary<string, string>? ParseTxtMetadata(IEnumerable<DnsAnswer> answers) {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DnsAnswer answer in answers) {
                foreach (string part in answer.DataStringsEscaped) {
                    int equals = part.IndexOf('=');
                    if (equals > 0) {
                        metadata[part.Substring(0, equals)] = part.Substring(equals + 1);
                    } else if (part.Length > 0) {
                        metadata[part] = string.Empty;
                    }
                }
            }

            return metadata.Count == 0 ? null : metadata;
        }

        private static bool TryParseSrv(DnsAnswer answer, out DnsSrvRecord? record) {
            record = null;
            string[] parts = answer.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4
                || !ushort.TryParse(parts[0], out ushort priority)
                || !ushort.TryParse(parts[1], out ushort weight)
                || !ushort.TryParse(parts[2], out ushort port)) {
                return false;
            }

            record = new DnsSrvRecord {
                Target = NormalizeDiscoveryName(parts[3]),
                Port = port,
                Priority = priority,
                Weight = weight
            };
            return true;
        }

        private static string BuildServiceType(string service, string protocol, string domain) {
            string normalizedService = NormalizeDiscoveryLabel(service, nameof(service));
            string normalizedProtocol = NormalizeDiscoveryLabel(protocol, nameof(protocol));
            string normalizedDomain = NormalizeDiscoveryDomain(domain, nameof(domain));
            return $"_{normalizedService}._{normalizedProtocol}.{normalizedDomain}";
        }

        private static string NormalizeDiscoveryLabel(string value, string parameterName) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentNullException(parameterName);
            }

            return value.Trim().Trim('_').Trim('.');
        }

        private static string NormalizeDiscoveryDomain(string value, string parameterName) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentNullException(parameterName);
            }

            return value.Trim().Trim('.');
        }

        private static string NormalizeDiscoveryName(string value) {
            return (value ?? string.Empty).Trim().TrimEnd('.');
        }

        private static string GetPtrTarget(DnsAnswer answer) {
            string raw = (answer.DataRaw ?? string.Empty).Trim();
            if (raw.Length > 0 && !raw.StartsWith("\\#", StringComparison.Ordinal)) {
                return NormalizeDiscoveryName(raw);
            }

            return NormalizeDiscoveryName(answer.Data);
        }
    }
}
