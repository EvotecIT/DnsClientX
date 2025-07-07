using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing streaming DNS resolution helpers.
    /// </summary>
    public partial class ClientX {
        /// <summary>
        /// Resolves multiple DNS record types for a single domain name and streams the responses.
        /// </summary>
        /// <param name="name">Domain name to resolve.</param>
        /// <param name="types">Record types to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all record types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">Maximum number of retries.</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async IAsyncEnumerable<DnsResponse> ResolveStream(
            string name,
            DnsRecordType[] types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (DnsRecordType type in types) {
                yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resolves multiple domain names for multiple DNS record types and streams the responses.
        /// </summary>
        /// <param name="names">Domain names to resolve.</param>
        /// <param name="types">Record types to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all record types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">Maximum number of retries.</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async IAsyncEnumerable<DnsResponse> ResolveStream(
            string[] names,
            DnsRecordType[] types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (string name in names) {
                foreach (DnsRecordType type in types) {
                    yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Resolves multiple domain names for a single DNS record type and streams the responses.
        /// </summary>
        /// <param name="names">Domain names to resolve.</param>
        /// <param name="type">Record type to resolve.</param>
        /// <param name="requestDnsSec">Whether to request DNSSEC data.</param>
        /// <param name="validateDnsSec">Whether to validate DNSSEC data.</param>
        /// <param name="returnAllTypes">Whether to return all record types.</param>
        /// <param name="retryOnTransient">Whether to retry on transient errors.</param>
        /// <param name="maxRetries">Maximum number of retries.</param>
        /// <param name="retryDelayMs">Delay between retries in milliseconds.</param>
        /// <param name="cancellationToken">Token used to cancel the operation.</param>
        public async IAsyncEnumerable<DnsResponse> ResolveStream(
            string[] names,
            DnsRecordType type,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (string name in names) {
                yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
