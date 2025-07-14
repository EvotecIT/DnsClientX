using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing streaming DNS resolution helpers.
    /// </summary>
    /// <remarks>
    /// Streaming allows large result sets to be processed without storing them all in memory.
    /// </remarks>
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
            var enumerator = ((IEnumerable<DnsRecordType>)types).GetEnumerator();
            try {
                while (enumerator.MoveNext()) {
                    DnsRecordType type = enumerator.Current;
                    yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            } finally {
                enumerator.Dispose();
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
            var nameEnumerator = ((IEnumerable<string>)names).GetEnumerator();
            try {
                while (nameEnumerator.MoveNext()) {
                    string name = nameEnumerator.Current;
                    var typeEnumerator = ((IEnumerable<DnsRecordType>)types).GetEnumerator();
                    try {
                        while (typeEnumerator.MoveNext()) {
                            DnsRecordType type = typeEnumerator.Current;
                            yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    } finally {
                        typeEnumerator.Dispose();
                    }
                }
            } finally {
                nameEnumerator.Dispose();
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
            var enumerator = ((IEnumerable<string>)names).GetEnumerator();
            try {
                while (enumerator.MoveNext()) {
                    string name = enumerator.Current;
                    yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            } finally {
                enumerator.Dispose();
            }
        }

        internal async IAsyncEnumerable<DnsResponse> ResolveStream(
            IEnumerable<string> names,
            IEnumerable<DnsRecordType> types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var nameEnumerator = names.GetEnumerator();
            try {
                while (nameEnumerator.MoveNext()) {
                    string name = nameEnumerator.Current!;
                    var typeEnumerator = types.GetEnumerator();
                    try {
                        while (typeEnumerator.MoveNext()) {
                            DnsRecordType type = typeEnumerator.Current;
                            yield return await Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    } finally {
                        typeEnumerator.Dispose();
                    }
                }
            } finally {
                nameEnumerator.Dispose();
            }
        }
    }
}
