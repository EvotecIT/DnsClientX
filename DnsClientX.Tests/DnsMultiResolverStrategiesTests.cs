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
        /// Ensures FastestWins warms endpoints and reuses the fastest endpoint for subsequent queries.
        /// </summary>
        [Fact]
        public async Task FastestWins_Warms_And_Uses_Cached_Fastest() {
            try {
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
                Assert.True(calls.TryGetValue("slow", out var vSlow) && vSlow == prevSlow);
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

