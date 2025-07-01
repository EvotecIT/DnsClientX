using System;
using System.Collections.Concurrent;

namespace DnsClientX {
    internal class DnsResponseCache {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

        private class CacheEntry {
            public CacheEntry(DnsResponse response, DateTimeOffset expiration) {
                Response = response;
                Expiration = expiration;
            }

            public DnsResponse Response { get; }
            public DateTimeOffset Expiration { get; }
        }

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

        public void Set(string key, DnsResponse response, TimeSpan ttl) {
            var entry = new CacheEntry(response, DateTimeOffset.UtcNow.Add(ttl));
            _cache[key] = entry;
        }
    }
}
