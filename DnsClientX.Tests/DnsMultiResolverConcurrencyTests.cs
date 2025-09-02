using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Concurrency and in-flight caps for DnsMultiResolver.
    /// </summary>
    public class DnsMultiResolverConcurrencyTests {
        [Fact]
        public async Task MaxParallelism_Caps_InFlight() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="e1", Port=53, Transport=Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 3 };
                int inFlight = 0, maxInFlight = 0;
                object gate = new object();
                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    int now;
                    lock (gate) { now = ++inFlight; if (now > maxInFlight) maxInFlight = now; }
                    try { await Task.Delay(50, ct); } finally { lock (gate) { inFlight--; } }
                    return new DnsResponse { Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } }, Status = DnsResponseCode.NoError };
                };
                var mr = new DnsMultiResolver(eps, opts);
                var names = new[] { "a","b","c","d","e","f" };
                await mr.QueryBatchAsync(names, DnsRecordType.A);
                Assert.True(maxInFlight <= 3, $"maxInFlight={maxInFlight}");
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }

        [Fact]
        public async Task PerEndpointMaxInFlight_Caps_Per_Endpoint() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="only", Port=53, Transport=Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 8, PerEndpointMaxInFlight = 2 };
                int inFlight = 0, maxInFlight = 0;
                object gate = new object();
                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    int now;
                    lock (gate) { now = ++inFlight; if (now > maxInFlight) maxInFlight = now; }
                    try { await Task.Delay(50, ct); } finally { lock (gate) { inFlight--; } }
                    return new DnsResponse { Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } }, Status = DnsResponseCode.NoError };
                };
                var mr = new DnsMultiResolver(eps, opts);
                var names = new[] { "a","b","c","d","e","f" };
                await mr.QueryBatchAsync(names, DnsRecordType.A);
                Assert.True(maxInFlight <= 2, $"maxInFlight={maxInFlight}");
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }
    }
}

