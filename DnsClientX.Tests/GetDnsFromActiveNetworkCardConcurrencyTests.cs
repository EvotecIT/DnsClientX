using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests concurrency when refreshing DNS server information.
    /// </summary>
    [Collection("NoParallel")]
    public class GetDnsFromActiveNetworkCardConcurrencyTests {
        /// <summary>
        /// Multiple concurrent refresh calls should return identical results.
        /// </summary>
        [Fact]
        public async Task RefreshConcurrentCalls_ShouldReturnConsistentResults() {
            // Isolate OS discovery and retain an independent expected resolver list.
            var expected = new List<string> { "1.1.1.1", "8.8.8.8" };
            SystemInformation.SetDnsServerProvider(() => new List<string>(expected));
            try {
                var tasks = Enumerable.Range(0, 20)
                    .Select(_ => Task.Run(() => SystemInformation.GetDnsFromActiveNetworkCard(refresh: true)));
                var results = await Task.WhenAll(tasks);

                Assert.Equal(20, results.Length);
                Assert.All(results, result => Assert.Equal(expected, result));
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }
    }
}
