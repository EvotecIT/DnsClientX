using System;
using System.Collections.Concurrent;

namespace DnsClientX {
    /// <summary>
    /// Simple in-memory cache for <see cref="DnsResponse"/> instances.
    /// </summary>
    internal class DnsResponseCache {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        /// <summary>
        /// Wrapper class storing cached response together with its expiration timestamp.
        /// </summary>
        private class CacheEntry {
            /// <summary>
            /// Initializes a new instance of the <see cref="CacheEntry"/> class.
            /// </summary>
            /// <param name="response">DNS response to cache.</param>
            /// <param name="expiration">Expiration time of the cached entry.</param>
            public CacheEntry(DnsResponse response, DateTimeOffset expiration) {
                Response = response;
                Expiration = expiration;
            }

            /// <summary>Gets the cached DNS response.</summary>
            public DnsResponse Response { get; }

            /// <summary>Gets the expiration time for the cached entry.</summary>
            public DateTimeOffset Expiration { get; }
        }

        /// <summary>
        /// Attempts to retrieve a cached response for a given key.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="response">Retrieved response if found and not expired.</param>
        /// <returns><c>true</c> if a valid entry was found; otherwise <c>false</c>.</returns>
        public bool TryGet(string key, out DnsResponse response) {
            if (_cache.TryGetValue(key, out var entry)) {
                if (DateTimeOffset.UtcNow < entry.Expiration) {
                    response = entry.Response;
                    return true;
                }
                _cache.TryRemove(key, out _);
            }
            response = default;
            return false;
        }

        /// <summary>
        /// Stores a response in the cache using the specified TTL value.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="response">Response to cache.</param>
        /// <param name="ttl">Time to keep the entry.</param>
        public void Set(string key, DnsResponse response, TimeSpan ttl) {
            var entry = new CacheEntry(response, DateTimeOffset.UtcNow.Add(ttl));
            _cache[key] = entry;
        }
    }
}
