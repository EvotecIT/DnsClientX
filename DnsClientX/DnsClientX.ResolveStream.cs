using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public partial class ClientX {
        /// <summary>
        /// Resolves multiple DNS record types for a single domain name and streams the responses.
        /// </summary>
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
