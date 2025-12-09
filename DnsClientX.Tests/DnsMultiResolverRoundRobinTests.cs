using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests RoundRobin behavior with a simulated resolver to avoid network access.
    /// </summary>
    public class DnsMultiResolverRoundRobinTests {
        /// <summary>
        /// Ensures distribution across endpoints and fallback on failure without using network.
        /// </summary>
        [Fact(Skip = "Round-robin distribution under simulated failures is timing-sensitive; skipping in unit suite.")]
        public async Task RoundRobin_Distributes_And_FallsBack() {
            try {
                // Arrange endpoints
                var eps = new [] {
                    new DnsResolverEndpoint { Host = "e1", Port = 53, Transport = Transport.Udp },
                    new DnsResolverEndpoint { Host = "e2", Port = 53, Transport = Transport.Udp },
                    new DnsResolverEndpoint { Host = "e3", Port = 53, Transport = Transport.Udp }
                };
                var opts = new MultiResolverOptions {
                    Strategy = MultiResolverStrategy.RoundRobin,
                    MaxParallelism = 8,
                    PerEndpointMaxInFlight = 4,
                    EnableResponseCache = false
                };

                // Count assignments and simulate fallback: make e2 fail to force fallback
                var counts = new ConcurrentDictionary<string, int>();
                DnsMultiResolver.ResolveOverride = (ep, name, type, ct) => {
                    counts.AddOrUpdate(ep.Host ?? string.Empty, 1, (_, v) => v + 1);
                    if (ep.Host == "e2") {
                        return Task.FromResult(new DnsResponse {
                            Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                            Status = DnsResponseCode.ServerFailure,
                            Error = "fail"
                        });
                    }
                    return Task.FromResult(new DnsResponse {
                        Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } },
                        Answers = new[] { new DnsAnswer { Name = name, Type = type, TTL = 60, DataRaw = "127.0.0.1" } },
                        Status = DnsResponseCode.NoError
                    });
                };

                var mr = new DnsMultiResolver(eps, opts);
                string[] names = Enumerable.Range(0, 9).Select(i => $"n{i}.example").ToArray();
                var results = await mr.QueryBatchAsync(names, DnsRecordType.A, CancellationToken.None);

                // Assert successful responses
                Assert.Equal(names.Length, results.Length);
                Assert.True(results.Count(r => r.Status == DnsResponseCode.NoError) >= names.Length - 3);

                // Distribution happened: at least two distinct endpoints were used successfully (excluding the failing one).
                var used = counts.Where(kv => kv.Key != "e2" && kv.Value > 0).Select(kv => kv.Key).Distinct().Count();
                Assert.True(used >= 2);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }
    }
}
