using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="DnsResponseCache"/> class.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsResponseCacheTests {
        /// <summary>
        /// Ensures cache items can be stored and retrieved.
        /// </summary>
        [Fact]
        public void ShouldStoreAndRetrieve() {
            var cache = new DnsResponseCache();
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Questions = new[] { new DnsQuestion { Name = "example.com", Type = DnsRecordType.A } },
                Answers = new[] { new DnsAnswer { Name = "example.com", Type = DnsRecordType.A, TTL = 60, DataRaw = "192.0.2.1" } }
            };
            cache.Set("a", response, TimeSpan.FromSeconds(1));
            Assert.True(cache.TryGet("a", out var cached));
            Assert.NotSame(response, cached);

            cached.Questions[0].Name = "changed.example";
            cached.Answers[0].Name = "changed.example";
            cached.Answers[0].TTL = 1;
            cached.Answers[0].DataRaw = "203.0.113.9";
            Assert.True(cache.TryGet("a", out var second));
            Assert.Equal("example.com", second.Questions[0].Name);
            Assert.Equal("example.com", second.Answers[0].Name);
            Assert.Equal(60, second.Answers[0].TTL);
            Assert.Equal("192.0.2.1", second.Answers[0].DataRaw);
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

        private sealed class GatedJsonResponseHandler : HttpMessageHandler {
            private readonly string _json;
            private readonly TaskCompletionSource<bool> _entered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _requestCount;

            internal GatedJsonResponseHandler(string json) => _json = json;
            internal Task Entered => _entered.Task;
            internal int RequestCount => Volatile.Read(ref _requestCount);
            internal void Release() => _release.TrySetResult(true);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken) {
                Interlocked.Increment(ref _requestCount);
                _entered.TrySetResult(true);
                await _release.Task.ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_json) };
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

        /// <summary>Concurrent exact-key misses share one network operation and return isolated clones.</summary>
        [Fact]
        public async Task ExactKeyMissesUseSingleFlightWithExplicitProvenance() {
            ClientX.ResetResponseCacheForTests();
            const string json = "{\"Status\":0,\"Question\":[{\"name\":\"single.example\",\"type\":1}],\"Answer\":[{\"name\":\"single.example\",\"type\":1,\"TTL\":60,\"data\":\"192.0.2.1\"}]}";
            var handler = new GatedJsonResponseHandler(json);
            using var client = new ClientX("https://resolver.example/dns-query",
                DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            InjectClient(client, new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri });

            Task<DnsResponse>[] tasks = Enumerable.Range(0, 32)
                .Select(_ => client.Resolve("single.example", retryOnTransient: false))
                .ToArray();
            await handler.Entered;
            await Task.Delay(50);
            handler.Release();
            DnsResponse[] responses = await Task.WhenAll(tasks);

            Assert.Equal(1, handler.RequestCount);
            Assert.Contains(responses, response => response.ResponseSource == DnsResponseSource.Network);
            Assert.Contains(responses, response => response.ResponseSource == DnsResponseSource.CoalescedNetwork);
            responses[0].Answers[0].DataRaw = "203.0.113.99";
            Assert.All(responses.Skip(1), response => Assert.Equal("192.0.2.1", response.Answers[0].DataRaw));

            DnsResponse cached = await client.Resolve("single.example", retryOnTransient: false);
            Assert.Equal(DnsResponseSource.Cache, cached.ResponseSource);
            Assert.True(cached.ServedFromCache);
            Assert.Equal(1, handler.RequestCount);
        }

        /// <summary>Canceling one waiter does not cancel the shared network operation or poison the cached result.</summary>
        [Fact]
        public async Task CallerCancellationDoesNotCancelSharedFlight() {
            ClientX.ResetResponseCacheForTests();
            const string json = "{\"Status\":0,\"Question\":[{\"name\":\"cancel.example\",\"type\":1}],\"Answer\":[{\"name\":\"cancel.example\",\"type\":1,\"TTL\":60,\"data\":\"192.0.2.2\"}]}";
            var handler = new GatedJsonResponseHandler(json);
            using var client = new ClientX("https://resolver.example/dns-query",
                DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            InjectClient(client, new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri });
            using var callerCancellation = new CancellationTokenSource();

            Task<DnsResponse> cancelled = client.Resolve("cancel.example", retryOnTransient: false,
                cancellationToken: callerCancellation.Token);
            await handler.Entered;
            Task<DnsResponse> survivor = client.Resolve("cancel.example", retryOnTransient: false);
            callerCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            handler.Release();

            DnsResponse response = await survivor;
            DnsResponse cached = await client.Resolve("cancel.example", retryOnTransient: false);

            Assert.Equal(DnsResponseSource.CoalescedNetwork, response.ResponseSource);
            Assert.Equal(DnsResponseSource.Cache, cached.ResponseSource);
            Assert.Equal(1, handler.RequestCount);
        }

        /// <summary>
        /// Verifies that a configured minimum never extends an authoritative DNS TTL.
        /// </summary>
        [Fact]
        public async Task ShouldNotExtendAuthoritativeTtlToConfiguredMinimum() {
            ClearCache();
            var json = "{\"Status\":0,\"Answer\":[{\"name\":\"example.com\",\"type\":1,\"TTL\":1,\"data\":\"1.1.1.1\"}]}";
            var handler = new JsonResponseHandler(json);
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            client.MaxCacheTtl = TimeSpan.FromSeconds(30);
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);

            var ttl = GetCachedTtl();
            Assert.InRange(ttl.TotalSeconds, 0, 1.1);
        }

        /// <summary>A projected address answer remains bounded by the CNAME TTL that led to it.</summary>
        [Fact]
        public async Task ProjectedAddressCacheDoesNotOutliveAlias() {
            ClearCache();
            var json = "{\"Status\":0,\"Answer\":[{\"name\":\"alias.example\",\"type\":5,\"TTL\":1,\"data\":\"target.example\"},{\"name\":\"target.example\",\"type\":1,\"TTL\":600,\"data\":\"192.0.2.1\"}]}";
            var handler = new JsonResponseHandler(json);
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON, enableCache: true);
            client.MaxCacheTtl = TimeSpan.FromMinutes(30);
            InjectClient(client, new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri });

            DnsResponse response = await client.Resolve("alias.example", DnsRecordType.A,
                returnAllTypes: false, retryOnTransient: false);

            Assert.Single(response.Answers);
            Assert.Equal(DnsRecordType.A, response.Answers[0].Type);
            Assert.InRange(GetCachedTtl().TotalSeconds, 0, 1.1);
        }

        /// <summary>
        /// Ensures capacity is a hard bound rather than an expired-entry cleanup hint.
        /// </summary>
        [Fact]
        public void ShouldEvictOldestEntryAtCapacity() {
            using var cache = new DnsResponseCache(cleanupThreshold: 2);
            var response = new DnsResponse { Status = DnsResponseCode.NoError };
            cache.Set("a", response, TimeSpan.FromMinutes(1));
            cache.Set("b", response, TimeSpan.FromMinutes(1));
            cache.Set("c", response, TimeSpan.FromMinutes(1));

            Assert.False(cache.TryGet("a", out _));
            Assert.True(cache.TryGet("b", out _));
            Assert.True(cache.TryGet("c", out _));
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
            client.MaxCacheTtl = TimeSpan.FromSeconds(30);
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);

            Assert.True(IsCacheEmpty());
        }

        /// <summary>
        /// Ensures the process-wide cache cannot cross client cache-lifetime or TLS-validation boundaries.
        /// </summary>
        [Fact]
        public void CacheKeySeparatesClientSecurityAndLifetimePolicies() {
            var configuration = new Configuration("https://resolver.example/dns-query", DnsRequestFormat.DnsOverHttps) {
                TlsServerName = "resolver.example"
            };
            string baseline = DnsCacheKeyBuilder.Build(configuration, "example.com", DnsRecordType.A,
                false, false, false, false, false, TimeSpan.FromMinutes(1), false);
            string longer = DnsCacheKeyBuilder.Build(configuration, "example.com", DnsRecordType.A,
                false, false, false, false, false, TimeSpan.FromHours(1), false);
            string insecure = DnsCacheKeyBuilder.Build(configuration, "example.com", DnsRecordType.A,
                false, false, false, false, false, TimeSpan.FromMinutes(1), true);
            configuration.TlsServerName = "different-resolver.example";
            string differentTlsIdentity = DnsCacheKeyBuilder.Build(configuration, "example.com", DnsRecordType.A,
                false, false, false, false, false, TimeSpan.FromMinutes(1), false);

            Assert.NotEqual(baseline, longer);
            Assert.NotEqual(baseline, insecure);
            Assert.NotEqual(baseline, differentTlsIdentity);
        }

        /// <summary>Cache entries never cross source address, interface, family, or fallback policy boundaries.</summary>
        [Fact]
        public void CacheKeySeparatesNetworkPathSelection() {
            var configuration = new Configuration("192.0.2.53", DnsRequestFormat.DnsOverUDP);
            string Key() => DnsCacheKeyBuilder.Build(configuration, "example.com", DnsRecordType.A,
                false, false, false, false, false, TimeSpan.FromMinutes(1), false);
            string baseline = Key();
            configuration.LocalEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            string sourceBound = Key();
            configuration.LocalEndPoint = null;
            configuration.MulticastInterfaceIndex = 4;
            string interfaceBound = Key();
            configuration.MulticastInterfaceIndex = null;
            configuration.PreferredAddressFamily = AddressFamily.InterNetworkV6;
            string ipv6Preferred = Key();
            configuration.PreferredAddressFamily = null;
            configuration.UseTcpFallback = false;
            string fallbackDisabled = Key();

            Assert.NotEqual(baseline, sourceBound);
            Assert.NotEqual(baseline, interfaceBound);
            Assert.NotEqual(baseline, ipv6Preferred);
            Assert.NotEqual(baseline, fallbackDisabled);
        }

        /// <summary>Locally validated entries cannot cross optional verifier instance boundaries.</summary>
        [Fact]
        public void CacheKeySeparatesDnsSecSignatureVerifiers() {
            var configuration = new Configuration("192.0.2.53", DnsRequestFormat.DnsOverUDP);
            string Key(bool validateDnsSec) => DnsCacheKeyBuilder.Build(configuration, "example.com", DnsRecordType.A,
                true, validateDnsSec, false, false, false, TimeSpan.FromMinutes(1), false);
            string withoutVerifier = Key(validateDnsSec: true);
            string unvalidated = Key(validateDnsSec: false);
            configuration.DnsSecSignatureVerifier = new FakeSignatureVerifier();
            string firstVerifier = Key(validateDnsSec: true);
            string unvalidatedWithVerifier = Key(validateDnsSec: false);
            configuration.DnsSecSignatureVerifier = new FakeSignatureVerifier();
            string secondVerifier = Key(validateDnsSec: true);

            Assert.NotEqual(withoutVerifier, firstVerifier);
            Assert.NotEqual(firstVerifier, secondVerifier);
            Assert.Equal(unvalidated, unvalidatedWithVerifier);
        }

        private sealed class FakeSignatureVerifier : IDnsSecSignatureVerifier {
            public string Name => "test";
            public bool SupportsAlgorithm(DnsKeyAlgorithm algorithm) => false;
            public bool Verify(DnsKeyAlgorithm algorithm, byte[] publicKey, byte[] data, byte[] signature) => false;
        }
    }
}
