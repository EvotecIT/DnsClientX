using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>Represents one explicit RFC 5011 root trust-anchor refresh.</summary>
    public sealed class DnsSecTrustAnchorRefreshResult {
        internal DnsSecTrustAnchorRefreshResult(DnsResponse response,
            DnsSecTrustAnchorStoreSnapshot? snapshot) {
            Response = response;
            Snapshot = snapshot;
        }

        /// <summary>Gets the DNSSEC-validated root DNSKEY response.</summary>
        public DnsResponse Response { get; }
        /// <summary>Gets the persisted state after a successful refresh, or null when validation failed.</summary>
        public DnsSecTrustAnchorStoreSnapshot? Snapshot { get; }
        /// <summary>Gets whether the refresh authenticated and persisted the observed root DNSKEY RRset.</summary>
        public bool Succeeded => Response.DnsSecValidationStatus == DnsSecValidationStatus.Secure && Snapshot != null;
    }

    public partial class ClientX {
        /// <summary>
        /// Performs an RFC 5011 active refresh of the root DNSKEY RRset and atomically updates the
        /// store configured by <see cref="Configuration.Rfc5011TrustAnchorStorePath"/>.
        /// </summary>
        /// <remarks>
        /// Applications should schedule the next call no later than <see cref="DnsSecTrustAnchorStoreSnapshot.NextRefreshUtc"/>.
        /// DnsClientX does not create a hidden background timer whose lifetime could outlive the owning application.
        /// </remarks>
        public async Task<DnsSecTrustAnchorRefreshResult> RefreshRootTrustAnchorsAsync(
            IEnumerable<string>? rootServers = null,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            Configuration queryConfiguration = EndpointConfiguration.CreateQuerySnapshot(".");
            string? path = queryConfiguration.Rfc5011TrustAnchorStorePath;
            if (string.IsNullOrWhiteSpace(path)) {
                throw new InvalidOperationException(
                    "Configuration.Rfc5011TrustAnchorStorePath must be set before refreshing managed trust anchors.");
            }

            DnsResponse response = await ResolveFromRootWithTelemetry(
                ".",
                DnsRecordType.DNSKEY,
                rootServers,
                queryConfiguration.IterativeMaxHops,
                queryConfiguration.Port,
                requestDnsSec: true,
                validateDnsSec: true,
                cancellationToken,
                queryConfiguration).ConfigureAwait(false);
            DnsSecTrustAnchorStoreSnapshot? snapshot = null;
            if (response.DnsSecValidationStatus == DnsSecValidationStatus.Secure && File.Exists(path)) {
                snapshot = DnsSecTrustAnchorStore.Load(path!);
            }
            return new DnsSecTrustAnchorRefreshResult(response, snapshot);
        }
    }
}
