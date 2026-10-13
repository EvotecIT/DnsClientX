using System;
using System.Collections.Generic;
using System.Threading;

namespace DnsClientX {
    /// <summary>Provides bounded streaming in query completion order.</summary>
    public partial class ClientX {
        /// <summary>
        /// Resolves record types for one name and yields responses as they complete.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="Configuration.MaxConcurrency"/> when positive, otherwise a limit of 32.
        /// Early disposal cancels and drains unfinished queries. Use ResolveStream for sequential input order.
        /// </remarks>
        public IAsyncEnumerable<DnsResponse> ResolveAsyncEnumerable(
            string name,
            DnsRecordType[] types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            CancellationToken cancellationToken = default) =>
            ResolveAsyncEnumerable(new[] { name }, types, requestDnsSec, validateDnsSec,
                returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken);

        /// <summary>
        /// Resolves names and record types with bounded concurrency and yields responses as they complete.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="Configuration.MaxConcurrency"/> when positive, otherwise a limit of 32.
        /// A consumer that stops early cancels and drains admitted queries; names beyond the
        /// bounded window are not started.
        /// </remarks>
        public IAsyncEnumerable<DnsResponse> ResolveAsyncEnumerable(
            string[] names,
            DnsRecordType[] types,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            CancellationToken cancellationToken = default) {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (types == null) throw new ArgumentNullException(nameof(types));
            ThrowIfDisposed();
            return DnsConcurrentStream.Execute(QueryPairs(names, types),
                (query, token) => Resolve(query.Name, query.Type, requestDnsSec, validateDnsSec,
                    returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken: token),
                EndpointConfiguration.MaxConcurrency, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Resolves names for one record type with bounded concurrency in completion order.
        /// </summary>
        /// <remarks>Early disposal cancels and drains unfinished queries.</remarks>
        public IAsyncEnumerable<DnsResponse> ResolveAsyncEnumerable(
            string[] names,
            DnsRecordType type,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            bool returnAllTypes = false,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 200,
            CancellationToken cancellationToken = default) =>
            ResolveAsyncEnumerable(names, new[] { type }, requestDnsSec, validateDnsSec,
                returnAllTypes, retryOnTransient, maxRetries, retryDelayMs, cancellationToken);

        private static IEnumerable<(string Name, DnsRecordType Type)> QueryPairs(
            IEnumerable<string> names, IEnumerable<DnsRecordType> types) {
            foreach (string name in names) {
                foreach (DnsRecordType type in types) yield return (name, type);
            }
        }
    }
}
