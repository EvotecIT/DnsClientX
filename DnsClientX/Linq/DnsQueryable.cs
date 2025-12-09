using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Linq {
    /// <summary>
    /// Lightweight LINQ-friendly wrapper that resolves DNS names and exposes the results as <see cref="IEnumerable{DnsAnswer}"/>.
    /// Uses only Enumerable-based LINQ operators (AOT-safe) and caches results for repeated enumeration.
    /// </summary>
    public class DnsQueryable : IEnumerable<DnsAnswer> {
        private readonly ClientX _client;
        private readonly List<string> _names;
        private readonly DnsRecordType _type;
        private readonly Lazy<Task<List<DnsAnswer>>> _lazyResults;

        /// <summary>
        /// Creates a DNS enumerable for the given names and record type.
        /// </summary>
        public DnsQueryable(ClientX client, IEnumerable<string> names, DnsRecordType type) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _names = names?.ToList() ?? throw new ArgumentNullException(nameof(names));
            _type = type;
            _lazyResults = new Lazy<Task<List<DnsAnswer>>>(ResolveAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private async Task<List<DnsAnswer>> ResolveAsync() {
            var responses = await Task.WhenAll(_names.Select(n => _client.Resolve(n, _type))).ConfigureAwait(false);
            return responses.SelectMany(r => r.Answers ?? Array.Empty<DnsAnswer>()).ToList();
        }

        /// <summary>
        /// Enumerates resolved DNS answers (resolution is performed once and cached).
        /// </summary>
        public IEnumerator<DnsAnswer> GetEnumerator() =>
            _lazyResults.Value.GetAwaiter().GetResult().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Executes the query asynchronously and returns all answers.
        /// </summary>
        public Task<List<DnsAnswer>> ToListAsync() => _lazyResults.Value;
    }
}
