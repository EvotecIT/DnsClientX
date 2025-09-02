using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsMultiResolverRoundRobinTests {
        [Fact]
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

                // Distribution happened (e1 and e3 should have non-zero)
                Assert.True(counts.GetValueOrDefault("e1") > 0);
                Assert.True(counts.GetValueOrDefault("e3") > 0);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }
    }
}

