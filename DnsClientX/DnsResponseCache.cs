using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace DnsClientX {
    /// <summary>
    /// Simple in-memory cache for <see cref="DnsResponse"/> instances.
    /// Stores responses together with their expiration times.
    /// </summary>
    internal class DnsResponseCache : IDisposable {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
        private readonly Timer _cleanupTimer;
        private readonly int _cleanupThreshold;
        private readonly TimeSpan _cleanupInterval;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsResponseCache"/> class.
        /// </summary>
        /// <param name="cleanupInterval">Interval for automatic cleanup.</param>
        /// <param name="cleanupThreshold">Maximum number of entries before a cleanup is triggered.</param>
        public DnsResponseCache(TimeSpan? cleanupInterval = null, int cleanupThreshold = 1000) {
            _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            _cleanupThreshold = cleanupThreshold;
            _cleanupTimer = new Timer(_ => Cleanup(), null, _cleanupInterval, _cleanupInterval);
        }

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
        public bool TryGet(string key, [NotNullWhen(true)] out DnsResponse? response) {
            if (_cache.TryGetValue(key, out var entry)) {
                if (DateTimeOffset.UtcNow < entry.Expiration) {
                    response = entry.Response;
                    return true;
                }
                _cache.TryRemove(key, out _);
            }
            response = default!;
            return false;
        }

        /// <summary>
        /// Removes expired entries from the cache.
        /// </summary>
        public void Cleanup() {
            if (_disposed) {
                return;
            }
            var now = DateTimeOffset.UtcNow;
            foreach (var item in _cache) {
                if (item.Value.Expiration <= now) {
                    _cache.TryRemove(item.Key, out _);
                }
            }
        }

        /// <summary>
        /// Stores a response in the cache using the specified TTL value.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="response">Response to cache.</param>
        /// <param name="ttl">Time to keep the entry.</param>
        /// <returns>None.</returns>
        public void Set(string key, DnsResponse response, TimeSpan ttl) {
            var entry = new CacheEntry(response, DateTimeOffset.UtcNow.Add(ttl));
            _cache[key] = entry;
            if (_cache.Count > _cleanupThreshold) {
                Cleanup();
            }
        }

        /// <inheritdoc/>
        public void Dispose() {
            if (_disposed) {
                return;
            }
            _disposed = true;
            _cleanupTimer.Dispose();
        }
    }
}
