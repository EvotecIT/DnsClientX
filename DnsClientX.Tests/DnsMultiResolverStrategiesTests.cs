using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests multi-resolver strategies: FirstSuccess, FastestWins, SequentialAll.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsMultiResolverStrategiesTests {
        private static DnsResponse Ok(string name, DnsRecordType type)
            => new DnsResponse {
                Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                Answers = new[] { new DnsAnswer { Name = name, Type = type, TTL = 30, DataRaw = "127.0.0.1" } },
                Status = DnsResponseCode.NoError
            };

        /// <summary>
        /// Ensures FirstSuccess returns as soon as the first successful response is available.
        /// </summary>
        [Fact]
        public async Task FirstSuccess_Picks_First_Valid_Response() {
            try {
                var eps = new[] {
                    new DnsResolverEndpoint { Host="s1", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="s2", Port=53, Transport=Transport.Udp }
                };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 2 };
                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    if (ep.Host == "s1") {
                        await Task.Delay(50, ct);
                        return Ok(name, type);
                    }
                    await Task.Delay(200, ct);
                    return Ok(name, type);
                };
                var mr = new DnsMultiResolver(eps, opts);
                var res = await mr.QueryAsync("example.com", DnsRecordType.A);
                Assert.Equal(DnsResponseCode.NoError, res.Status);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }

        /// <summary>
        /// Ensures FirstSuccess backfills its parallel window immediately after a failed endpoint completes.
        /// </summary>
        [Fact]
        public async Task FirstSuccess_StartsNextQueuedEndpoint_AsSoonAsFailureCompletes() {
            try {
                var eps = new[] {
                    new DnsResolverEndpoint { Host="slow-fail", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="fast-fail", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="late-success", Port=53, Transport=Transport.Udp }
                };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 2 };
                var slowGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var successStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    if (ep.Host == "slow-fail") {
                        var released = await Task.WhenAny(slowGate.Task, Task.Delay(Timeout.Infinite, ct));
                        await released.ConfigureAwait(false);
                        return new DnsResponse {
                            Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                            Status = DnsResponseCode.ServerFailure,
                            Error = "slow failure",
                            ErrorCode = DnsQueryErrorCode.ServFail
                        };
                    }

                    if (ep.Host == "fast-fail") {
                        await Task.Delay(30, ct);
                        return new DnsResponse {
                            Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                            Status = DnsResponseCode.ServerFailure,
                            Error = "fast failure",
                            ErrorCode = DnsQueryErrorCode.ServFail
                        };
                    }

                    successStarted.TrySetResult(true);
                    return Ok(name, type);
                };

                var mr = new DnsMultiResolver(eps, opts);
                var queryTask = mr.QueryAsync("example.com", DnsRecordType.A);

                var startedTask = await Task.WhenAny(successStarted.Task, Task.Delay(TimeSpan.FromSeconds(1)));
                Assert.Same(successStarted.Task, startedTask);

                slowGate.TrySetResult(true);
                var res = await queryTask;
                Assert.Equal(DnsResponseCode.NoError, res.Status);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }

        /// <summary>
        /// Ensures FastestWins warms endpoints and reuses the fastest endpoint for subsequent queries.
        /// </summary>
        [Fact]
        public async Task FastestWins_Warms_And_Uses_Cached_Fastest() {
            try {
                DnsMultiResolver.ClearFastestCache();
                var eps = new[] {
                    new DnsResolverEndpoint { Host="fast", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="slow", Port=53, Transport=Transport.Udp }
                };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FastestWins, MaxParallelism = 2, FastestCacheDuration = TimeSpan.FromMinutes(5) };
                var calls = new ConcurrentDictionary<string,int>();
                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    calls.AddOrUpdate(ep.Host ?? string.Empty, 1, (_, v) => v + 1);
                    if (ep.Host == "slow") await Task.Delay(80, ct); else await Task.Delay(10, ct);
                    return Ok(name, type);
                };
                var mr = new DnsMultiResolver(eps, opts);
                var r1 = await mr.QueryAsync("a.com", DnsRecordType.A);
                Assert.Equal(DnsResponseCode.NoError, r1.Status);
                // Second query should hit only the cached fastest endpoint
                var prevFast = calls.TryGetValue("fast", out var v1) ? v1 : 0;
                var prevSlow = calls.TryGetValue("slow", out var v2) ? v2 : 0;
                var r2 = await mr.QueryAsync("b.com", DnsRecordType.A);
                Assert.Equal(DnsResponseCode.NoError, r2.Status);
                Assert.True(calls.TryGetValue("fast", out var vFast) && vFast > prevFast);
                // Ensure the cached call did not heavily favor the slow endpoint
                if (calls.TryGetValue("slow", out var vSlow)) {
                    Assert.True(vSlow <= vFast);
                }
            } finally { DnsMultiResolver.ResolveOverride = null; DnsMultiResolver.ClearFastestCache(); }
        }

        /// <summary>
        /// Ensures FastestWins cache differentiates endpoints that share a URL but use different request formats.
        /// </summary>
        [Fact]
        public async Task FastestWins_CacheKey_Separates_RequestFormats() {
            try {
                DnsMultiResolver.ClearFastestCache();
                var jsonEndpoint = new DnsResolverEndpoint {
                    Host = "1.1.1.1",
                    Port = 443,
                    Transport = Transport.Doh,
                    DohUrl = new Uri("https://1.1.1.1/dns-query"),
                    RequestFormat = DnsRequestFormat.DnsOverHttpsJSON
                };
                var wireEndpoint = new DnsResolverEndpoint {
                    Host = "1.1.1.1",
                    Port = 443,
                    Transport = Transport.Doh,
                    DohUrl = new Uri("https://1.1.1.1/dns-query"),
                    RequestFormat = DnsRequestFormat.DnsOverHttps
                };

                var calls = new ConcurrentDictionary<string, int>();
                string Key(DnsResolverEndpoint ep) => ep.RequestFormat?.ToString() ?? string.Empty;

                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    calls.AddOrUpdate(Key(ep), 1, (_, value) => value + 1);
                    if (ep.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON) {
                        await Task.Delay(50, ct);
                    } else {
                        await Task.Delay(5, ct);
                    }

                    return Ok(name, type);
                };

                var first = new DnsMultiResolver(
                    new[] { jsonEndpoint, wireEndpoint },
                    new MultiResolverOptions { Strategy = MultiResolverStrategy.FastestWins, MaxParallelism = 2, FastestCacheDuration = TimeSpan.FromMinutes(5) });
                var firstResponse = await first.QueryAsync("example.com", DnsRecordType.A);
                Assert.Equal(DnsResponseCode.NoError, firstResponse.Status);

                int jsonBefore = calls.TryGetValue(DnsRequestFormat.DnsOverHttpsJSON.ToString(), out var jsonCount) ? jsonCount : 0;
                int wireBefore = calls.TryGetValue(DnsRequestFormat.DnsOverHttps.ToString(), out var wireCount) ? wireCount : 0;

                var second = new DnsMultiResolver(
                    new[] { wireEndpoint, jsonEndpoint },
                    new MultiResolverOptions { Strategy = MultiResolverStrategy.FastestWins, MaxParallelism = 2, FastestCacheDuration = TimeSpan.FromMinutes(5) });
                var secondResponse = await second.QueryAsync("example.org", DnsRecordType.A);

                Assert.Equal(DnsResponseCode.NoError, secondResponse.Status);
                Assert.Equal(jsonBefore, calls.TryGetValue(DnsRequestFormat.DnsOverHttpsJSON.ToString(), out jsonCount) ? jsonCount : 0);
                Assert.True((calls.TryGetValue(DnsRequestFormat.DnsOverHttps.ToString(), out wireCount) ? wireCount : 0) > wireBefore);
            } finally { DnsMultiResolver.ResolveOverride = null; DnsMultiResolver.ClearFastestCache(); }
        }

        /// <summary>
        /// Ensures SequentialAll tries endpoints in order and returns the first success.
        /// </summary>
        [Fact]
        public async Task SequentialAll_Tries_In_Order_And_Returns_Success() {
            try {
                var eps = new[] {
                    new DnsResolverEndpoint { Host="bad", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="ok", Port=53, Transport=Transport.Udp }
                };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.SequentialAll };
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => {
                    if (ep.Host == "bad") {
                        return Task.FromResult(new DnsResponse { Questions = new[]{ new DnsQuestion { Name=name, Type=type, OriginalName=name } }, Status = DnsResponseCode.ServerFailure, Error = "fail", ErrorCode = DnsQueryErrorCode.ServFail });
                    }
                    return Task.FromResult(Ok(name, type));
                };
                var mr = new DnsMultiResolver(eps, opts);
                var res = await mr.QueryAsync("example.com", DnsRecordType.A);
                Assert.Equal(DnsResponseCode.NoError, res.Status);
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }
    }
}

