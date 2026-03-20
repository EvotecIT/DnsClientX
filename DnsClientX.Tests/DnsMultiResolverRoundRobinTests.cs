using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests RoundRobin behavior with a simulated resolver to avoid network access.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsMultiResolverRoundRobinTests {
        /// <summary>
        /// Ensures distribution across endpoints and fallback on failure without using network.
        /// </summary>
        [Fact]
        public async Task RoundRobin_Distributes_And_FallsBack() {
            try {
                var eps = new [] {
                    new DnsResolverEndpoint { Host = "e1", Port = 53, Transport = Transport.Udp },
                    new DnsResolverEndpoint { Host = "e2", Port = 53, Transport = Transport.Udp },
                    new DnsResolverEndpoint { Host = "e3", Port = 53, Transport = Transport.Udp }
                };
                var opts = new MultiResolverOptions {
                    Strategy = MultiResolverStrategy.RoundRobin,
                    MaxParallelism = 1,
                    EnableResponseCache = false
                };

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
                var r1 = await mr.QueryAsync("n1.example", DnsRecordType.A, CancellationToken.None);
                var r2 = await mr.QueryAsync("n2.example", DnsRecordType.A, CancellationToken.None);
                var r3 = await mr.QueryAsync("n3.example", DnsRecordType.A, CancellationToken.None);

                Assert.Equal(DnsResponseCode.NoError, r1.Status);
                Assert.Equal(DnsResponseCode.NoError, r2.Status);
                Assert.Equal(DnsResponseCode.NoError, r3.Status);
                Assert.Equal("e1", r1.UsedEndpoint?.Host);
                Assert.Equal("e1", r2.UsedEndpoint?.Host); // e2 fails and falls back to first
                Assert.Equal("e3", r3.UsedEndpoint?.Host);
                Assert.Equal(2, counts["e1"]);
                Assert.Equal(1, counts["e2"]);
                Assert.Equal(1, counts["e3"]);
            } finally {
                DnsMultiResolver.ResolveOverride = null;
            }
        }
    }
}
