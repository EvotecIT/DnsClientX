using System;

namespace DnsClientX {
    public partial class ClientX {
        private static readonly Caching.DnsCache _dnsCache = new();

        private bool TryGetCachedResponse(string key, out DnsResponse response) =>
            _dnsCache.TryGet(key, out response);

        private void StoreResponseInCache(string key, DnsResponse response, TimeSpan ttl) =>
            _dnsCache.Set(key, response, ttl);
    }
}
