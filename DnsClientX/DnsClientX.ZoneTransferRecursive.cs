using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing recursive authoritative AXFR helpers.
    /// </summary>
    public partial class ClientX {
        private readonly record struct ZoneTransferCandidate(string Authority, string Server);

        /// <summary>
        /// Discovers authoritative name servers for a zone and attempts AXFR against them until one succeeds.
        /// </summary>
        /// <param name="zone">Zone name to transfer.</param>
        /// <param name="port">TCP port to use for AXFR.</param>
        /// <param name="retryOnTransient">Whether to retry individual AXFR attempts on transient failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts per authoritative target.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retry attempts.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Details about the successful authoritative transfer.</returns>
        public async Task<RecursiveZoneTransferResult> ZoneTransferRecursiveAsync(
            string zone,
            int port = 53,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(zone)) {
                throw new ArgumentNullException(nameof(zone));
            }

            if (port < 1 || port > 65535) {
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
            }

            string normalizedZone = zone.Trim().TrimEnd('.');
            (string[] authorities, ZoneTransferCandidate[] candidates) = await DiscoverZoneTransferCandidatesAsync(normalizedZone, cancellationToken).ConfigureAwait(false);
            if (candidates.Length == 0) {
                throw new DnsClientException($"No authoritative AXFR targets were discovered for zone {normalizedZone}.");
            }

            var triedServers = new List<string>(candidates.Length);
            var errors = new List<string>(candidates.Length);

            foreach (ZoneTransferCandidate candidate in candidates) {
                cancellationToken.ThrowIfCancellationRequested();
                triedServers.Add(candidate.Server);

                using var transferClient = CreateRecursiveZoneTransferClient(candidate.Server, port);
                try {
                    ZoneTransferResult[] recordSets = await transferClient.ZoneTransferAsync(
                        normalizedZone,
                        retryOnTransient,
                        maxRetries,
                        retryDelayMs,
                        cancellationToken).ConfigureAwait(false);

                    return new RecursiveZoneTransferResult {
                        Zone = normalizedZone,
                        SelectedAuthority = candidate.Authority,
                        SelectedServer = candidate.Server,
                        Port = port,
                        Authorities = authorities,
                        TriedServers = triedServers.ToArray(),
                        RecordSets = recordSets
                    };
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) when (ex is DnsClientException || ex is TimeoutException || ex is SocketException) {
                    errors.Add($"{candidate.Server}: {ex.Message}");
                }
            }

            throw new DnsClientException(
                $"Recursive zone transfer failed for {normalizedZone}. Tried {string.Join(", ", triedServers)}. {string.Join(" | ", errors)}");
        }

        /// <summary>
        /// Synchronously discovers authoritative name servers for a zone and attempts AXFR against them.
        /// </summary>
        public RecursiveZoneTransferResult ZoneTransferRecursiveSync(
            string zone,
            int port = 53,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            CancellationToken cancellationToken = default) {
            return ZoneTransferRecursiveAsync(zone, port, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).RunSync(cancellationToken);
        }

        private async Task<(string[] Authorities, ZoneTransferCandidate[] Candidates)> DiscoverZoneTransferCandidatesAsync(
            string zone,
            CancellationToken cancellationToken) {
            DnsResponse nsResponse = await Resolve(
                zone,
                DnsRecordType.NS,
                requestDnsSec: false,
                validateDnsSec: false,
                returnAllTypes: false,
                retryOnTransient: true,
                maxRetries: 1,
                retryDelayMs: 0,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            string[] authorities = (nsResponse.Answers ?? Array.Empty<DnsAnswer>())
                .Where(answer => answer.Type == DnsRecordType.NS)
                .Select(answer => answer.Data.TrimEnd('.'))
                .Where(authority => !string.IsNullOrWhiteSpace(authority))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (authorities.Length == 0) {
                throw new DnsClientException($"No authoritative NS records were returned for zone {zone}.");
            }

            var candidates = new List<ZoneTransferCandidate>();
            var seenServers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DnsAnswer[] additional = nsResponse.Additional ?? Array.Empty<DnsAnswer>();

            foreach (string authority in authorities) {
                int candidateCountBefore = candidates.Count;

                foreach (DnsAnswer glue in additional.Where(answer =>
                             (answer.Type == DnsRecordType.A || answer.Type == DnsRecordType.AAAA) &&
                             string.Equals(answer.Name, authority, StringComparison.OrdinalIgnoreCase))) {
                    if (TryGetAddressValue(glue, out string? glueAddress) && glueAddress is not null) {
                        AddZoneTransferCandidate(candidates, seenServers, authority, glueAddress);
                    }
                }

                await AddResolvedAuthorityCandidatesAsync(authority, candidates, seenServers, cancellationToken).ConfigureAwait(false);
                if (candidates.Count == candidateCountBefore) {
                    AddZoneTransferCandidate(candidates, seenServers, authority, authority);
                }
            }

            return (authorities, candidates.ToArray());
        }

        private async Task AddResolvedAuthorityCandidatesAsync(
            string authority,
            List<ZoneTransferCandidate> candidates,
            HashSet<string> seenServers,
            CancellationToken cancellationToken) {
            foreach (DnsRecordType recordType in new[] { DnsRecordType.A, DnsRecordType.AAAA }) {
                try {
                    DnsResponse response = await Resolve(
                        authority,
                        recordType,
                        requestDnsSec: false,
                        validateDnsSec: false,
                        returnAllTypes: false,
                        retryOnTransient: true,
                        maxRetries: 1,
                        retryDelayMs: 0,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    foreach (DnsAnswer answer in response.Answers ?? Array.Empty<DnsAnswer>()) {
                        if (TryGetAddressValue(answer, out string? address) && address is not null) {
                            AddZoneTransferCandidate(candidates, seenServers, authority, address);
                        }
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch {
                    // Ignore resolution failures and continue to other candidates.
                }
            }
        }

        private static bool TryGetAddressValue(DnsAnswer answer, out string? address) {
            address = null;
            if (answer.Type != DnsRecordType.A && answer.Type != DnsRecordType.AAAA) {
                return false;
            }

            if (IPAddress.TryParse(answer.Data, out IPAddress? parsed)) {
                address = parsed.ToString();
                return true;
            }

            if (!string.IsNullOrWhiteSpace(answer.DataRaw)) {
                try {
                    byte[] bytes = Convert.FromBase64String(answer.DataRaw);
                    if (bytes.Length == 4 || bytes.Length == 16) {
                        address = new IPAddress(bytes).ToString();
                        return true;
                    }
                } catch {
                    // Ignore malformed or non-base64 payloads.
                }
            }

            return false;
        }

        private static void AddZoneTransferCandidate(
            List<ZoneTransferCandidate> candidates,
            HashSet<string> seenServers,
            string authority,
            string server) {
            string normalizedServer = server?.Trim().TrimEnd('.') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedServer) || !seenServers.Add(normalizedServer)) {
                return;
            }

            candidates.Add(new ZoneTransferCandidate(authority, normalizedServer));
        }

        private ClientX CreateRecursiveZoneTransferClient(string server, int port) {
            var client = new ClientX(
                server,
                DnsRequestFormat.DnsOverTCP,
                EndpointConfiguration.TimeOut,
                EndpointConfiguration.UserAgent,
                EndpointConfiguration.HttpVersion,
                IgnoreCertificateErrors,
                enableCache: false,
                useTcpFallback: false,
                webProxy: _webProxy,
                maxConnectionsPerServer: EndpointConfiguration.MaxConnectionsPerServer);
            client.EndpointConfiguration.Port = port;
            return client;
        }
    }
}
