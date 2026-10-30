using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Concurrency and in-flight caps for DnsMultiResolver.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsMultiResolverConcurrencyTests {
        /// <summary>
        /// Verifies that QueryBatchAsync honors the MaxParallelism cap across all endpoints.
        /// </summary>
        [Fact]
        public async Task MaxParallelism_Caps_InFlight() {
            try {
                var eps = new[] {
                    new DnsResolverEndpoint { Host="e1", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="e2", Port=53, Transport=Transport.Udp },
                    new DnsResolverEndpoint { Host="e3", Port=53, Transport=Transport.Udp }
                };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 3 };
                int inFlight = 0, maxInFlight = 0;
                object gate = new object();
                var dispatched = new ConcurrentDictionary<string, int>();
                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    dispatched.AddOrUpdate(name, 1, (_, count) => count + 1);
                    int now;
                    lock (gate) { now = ++inFlight; if (now > maxInFlight) maxInFlight = now; }
                    try { await Task.Delay(50, ct); } finally { lock (gate) { inFlight--; } }
                    return new DnsResponse { Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } }, Status = DnsResponseCode.NoError };
                };
                using var mr = new DnsMultiResolver(eps, opts);
                var names = new[] { "a","b","c","d","e","f" };
                var responses = await mr.QueryBatchAsync(names, DnsRecordType.A);
                Assert.Equal(names.Length, responses.Length);
                Assert.Equal(names, responses.Select(response => Assert.Single(response.Questions).Name));
                Assert.All(responses, response => Assert.Equal(DnsResponseCode.NoError, response.Status));
                Assert.Equal(names.OrderBy(name => name), dispatched.Keys.OrderBy(name => name));
                Assert.True(maxInFlight > 0);
                Assert.True(maxInFlight <= opts.MaxParallelism, $"maxInFlight={maxInFlight}");
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }

        /// <summary>
        /// Verifies that PerEndpointMaxInFlight limits concurrent requests per single endpoint.
        /// </summary>
        [Fact]
        public async Task PerEndpointMaxInFlight_Caps_Per_Endpoint() {
            try {
                var eps = new[] { new DnsResolverEndpoint { Host="only", Port=53, Transport=Transport.Udp } };
                var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 8, PerEndpointMaxInFlight = 2 };
                int inFlight = 0, maxInFlight = 0;
                object gate = new object();
                var dispatched = new ConcurrentDictionary<string, int>();
                DnsMultiResolver.ResolveOverride = async (ep, name, type, ct) => {
                    dispatched.AddOrUpdate(name, 1, (_, count) => count + 1);
                    int now;
                    lock (gate) { now = ++inFlight; if (now > maxInFlight) maxInFlight = now; }
                    try { await Task.Delay(50, ct); } finally { lock (gate) { inFlight--; } }
                    return new DnsResponse { Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name } }, Status = DnsResponseCode.NoError };
                };
                using var mr = new DnsMultiResolver(eps, opts);
                var names = new[] { "a","b","c","d","e","f" };
                var responses = await mr.QueryBatchAsync(names, DnsRecordType.A);
                Assert.Equal(names.Length, responses.Length);
                Assert.Equal(names, responses.Select(response => Assert.Single(response.Questions).Name));
                Assert.All(responses, response => Assert.Equal(DnsResponseCode.NoError, response.Status));
                Assert.Equal(names.OrderBy(name => name), dispatched.Keys.OrderBy(name => name));
                Assert.True(maxInFlight > 0);
                Assert.All(dispatched.Values, count => Assert.Equal(1, count));
                Assert.True(maxInFlight <= opts.PerEndpointMaxInFlight, $"maxInFlight={maxInFlight}");
            } finally { DnsMultiResolver.ResolveOverride = null; }
        }
    }
}

