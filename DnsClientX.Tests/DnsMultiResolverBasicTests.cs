using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Basic integration-style validation for <see cref="DnsMultiResolver"/>, skipped by default.
    /// </summary>
    public class DnsMultiResolverBasicTests {
        /// <summary>
        /// Batch queries preserve input order and isolate per-item failures.
        /// </summary>
        [Fact(Skip = "Integration test - network dependent; run locally if needed")]
        public async Task Batch_PreservesOrder_And_IsolatesFailures() {
            var eps = EndpointParser.TryParseMany(new []{ "1.1.1.1:53", "8.8.8.8:53" }, out var errors);
            Assert.Empty(errors);
            var opts = new MultiResolverOptions { Strategy = MultiResolverStrategy.FirstSuccess, MaxParallelism = 2 };
            var mr = new DnsMultiResolver(eps, opts);

            string[] names = new[] { "example.com", "cloudflare.com", "nonexistent.name.invalid." };
            var res = await mr.QueryBatchAsync(names, DnsRecordType.A, CancellationToken.None);
            Assert.Equal(names.Length, res.Length);
            Assert.Equal("example.com", res[0].Questions[0].OriginalName);
            Assert.Equal("cloudflare.com", res[1].Questions[0].OriginalName);
            Assert.Equal("nonexistent.name.invalid.", res[2].Questions[0].OriginalName);
        }
    }
}
