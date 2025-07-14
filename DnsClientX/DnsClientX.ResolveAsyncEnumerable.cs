using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class providing concurrent resolve helpers.
    /// </summary>
    /// <remarks>
    /// Enables streaming of results as each DNS query completes.
    /// </remarks>
    public partial class ClientX {
        /// <summary>
        /// Resolves multiple DNS record types for a single domain name concurrently and yields responses as they complete.
        /// </summary>
        public async IAsyncEnumerable<DnsResponse> ResolveAsyncEnumerable(
            string name,
            DnsRecordType[] types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var tasks = new List<Task<DnsResponse>>();

            foreach (DnsRecordType type in types) {
                tasks.Add(Resolve(name, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken));
            }

            while (tasks.Count > 0) {
                Task<DnsResponse> finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finished);
                yield return await finished.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resolves multiple domain names and DNS record types concurrently and yields responses as they complete.
        /// </summary>
        public async IAsyncEnumerable<DnsResponse> ResolveAsyncEnumerable(
            string[] names,
            DnsRecordType[] types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var tasks = new List<Task<DnsResponse>>();

            foreach (string n in names) {
                foreach (DnsRecordType type in types) {
                    tasks.Add(Resolve(n, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken));
                }
            }

            while (tasks.Count > 0) {
                Task<DnsResponse> finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finished);
                yield return await finished.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Resolves multiple domain names for a single DNS record type concurrently and yields responses as they complete.
        /// </summary>
        public async IAsyncEnumerable<DnsResponse> ResolveAsyncEnumerable(
            string[] names,
            DnsRecordType type,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var tasks = new List<Task<DnsResponse>>();

            foreach (string n in names) {
                tasks.Add(Resolve(n, type, requestDnsSec, validateDnsSec, returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: cancellationToken));
            }

            while (tasks.Count > 0) {
                Task<DnsResponse> finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finished);
                yield return await finished.ConfigureAwait(false);
            }
        }
    }
}
