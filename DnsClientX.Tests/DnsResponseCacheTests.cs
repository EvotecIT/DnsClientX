using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsResponseCache"/> class.
    /// </summary>
    public class DnsResponseCacheTests {
        /// <summary>
        /// Ensures cache items can be stored and retrieved.
        /// </summary>
        [Fact]
        public void ShouldStoreAndRetrieve() {
            var cache = new DnsResponseCache();
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            cache.Set("a", response, TimeSpan.FromSeconds(1));
            Assert.True(cache.TryGet("a", out var cached));
            Assert.Same(response, cached);
        }

        /// <summary>
        /// Items should be removed once their TTL expires.
        /// </summary>
        [Fact]
        public void ShouldEvictAfterExpiration() {
            var cache = new DnsResponseCache();
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            cache.Set("a", response, TimeSpan.FromMilliseconds(100));
            Thread.Sleep(150);
            Assert.False(cache.TryGet("a", out _));
        }

        /// <summary>
        /// Calling <see cref="DnsResponseCache.Cleanup"/> should remove expired entries.
        /// </summary>
        [Fact]
        public void ShouldCleanupExpiredEntries() {
            var cache = new DnsResponseCache();
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            cache.Set("a", response, TimeSpan.FromMilliseconds(10));
            cache.Set("b", response, TimeSpan.FromMilliseconds(10));
            Thread.Sleep(20);
            cache.Cleanup();
            Assert.False(cache.TryGet("a", out _));
            Assert.False(cache.TryGet("b", out _));
        }

        /// <summary>
        /// Verifies that constructing <see cref="ClientX"/> with caching enabled sets the property.
        /// </summary>
        [Fact]
        public void ClientConstructorEnablesCache() {
            using var client = new ClientX(enableCache: true);
            PropertyInfo property = typeof(ClientX).GetProperty("CacheEnabled")!;
            Assert.True((bool)property.GetValue(client)!);
        }

        private class JsonResponseHandler : HttpMessageHandler {
            private readonly string _json;
            public JsonResponseHandler(string json) => _json = json;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_json) };
                return Task.FromResult(response);
            }
        }

        private static void InjectClient(ClientX client, HttpClient httpClient) {
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(client)!;
            clients[client.EndpointConfiguration.SelectionStrategy] = httpClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(client, httpClient);
        }

        private static void ClearCache() {
            var cacheField = typeof(ClientX).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)!;
            var cache = cacheField.GetValue(null)!;
            var dictField = cache.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var dict = dictField.GetValue(cache)!;
            dict.GetType().GetMethod("Clear")!.Invoke(dict, null);
        }

        private static TimeSpan GetCachedTtl() {
            var cacheField = typeof(ClientX).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)!;
            var cache = cacheField.GetValue(null)!;
            var dictField = cache.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var dict = dictField.GetValue(cache)!;
            var values = (System.Collections.IEnumerable)dict.GetType().GetProperty("Values")!.GetValue(dict)!;
            var enumerator = values.GetEnumerator();
            enumerator.MoveNext();
            var entry = enumerator.Current;
            var expiration = (DateTimeOffset)entry.GetType().GetProperty("Expiration")!.GetValue(entry)!;
            return expiration - DateTimeOffset.UtcNow;
        }

        private static bool IsCacheEmpty() {
            var cacheField = typeof(ClientX).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)!;
            var cache = cacheField.GetValue(null)!;
            var dictField = cache.GetType().GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var dict = dictField.GetValue(cache)!;
            int count = (int)dict.GetType().GetProperty("Count")!.GetValue(dict)!;
            return count == 0;
        }

        /// <summary>
        /// Verifies that cache TTL values below the minimum are clamped.
        /// </summary>
        [Fact]
        public async Task ShouldClampToMinCacheTtl() {
            ClearCache();
            var json = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com\",\"type\":1,\"TTL\":1,\"data\":\"1.1.1.1\"}]}";
            var handler = new JsonResponseHandler(json);
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            client.MinCacheTtl = TimeSpan.FromSeconds(5);
            client.MaxCacheTtl = TimeSpan.FromSeconds(30);
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);

            var ttl = GetCachedTtl();
            Assert.InRange(Math.Abs((ttl - client.MinCacheTtl).TotalSeconds), 0, 1);
        }

        /// <summary>
        /// Verifies that cache TTL values above the maximum are clamped.
        /// </summary>
        [Fact]
        public async Task ShouldClampToMaxCacheTtl() {
            ClearCache();
            var json = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com\",\"type\":1,\"TTL\":600,\"data\":\"1.1.1.1\"}]}";
            var handler = new JsonResponseHandler(json);
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            client.MinCacheTtl = TimeSpan.FromSeconds(5);
            client.MaxCacheTtl = TimeSpan.FromSeconds(10);
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);

            var ttl = GetCachedTtl();
            Assert.InRange(Math.Abs((ttl - client.MaxCacheTtl).TotalSeconds), 0, 1);
        }

        /// <summary>
        /// Ensures responses with a TTL of zero are not cached.
        /// </summary>
        [Fact]
        public async Task ShouldNotCacheWhenTtlZero() {
            ClearCache();
            var json = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com\",\"type\":1,\"TTL\":0,\"data\":\"1.1.1.1\"}]}";
            var handler = new JsonResponseHandler(json);
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            client.MinCacheTtl = TimeSpan.FromSeconds(5);
            client.MaxCacheTtl = TimeSpan.FromSeconds(30);
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);

            Assert.True(IsCacheEmpty());
        }
    }
}
