using System;

namespace DnsClientX {
    public partial class ClientX {
        private bool TryGetCachedResponse(string key, out DnsResponse response) =>
            _cache.TryGet(key, out response);

        private void StoreResponseInCache(string key, DnsResponse response, TimeSpan ttl) =>
            _cache.Set(key, response, ttl);
    }
}
