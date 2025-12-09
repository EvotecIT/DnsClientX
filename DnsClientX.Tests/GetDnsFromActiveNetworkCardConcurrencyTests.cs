using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests concurrency when refreshing DNS server information.
    /// </summary>
    public class GetDnsFromActiveNetworkCardConcurrencyTests {
        /// <summary>
        /// Multiple concurrent refresh calls should return identical results.
        /// </summary>
        [Fact]
        public async Task RefreshConcurrentCalls_ShouldReturnConsistentResults() {
            // Use a custom provider but assert consistency rather than specific values (avoids env differences).
            var expected = new List<string> { "1.1.1.1", "8.8.8.8" };
            SystemInformation.SetDnsServerProvider(() => new List<string>(expected));
            try {
                var tasks = Enumerable.Range(0, 20)
                    .Select(_ => Task.Run(() => SystemInformation.GetDnsFromActiveNetworkCard(refresh: true)));
                var results = await Task.WhenAll(tasks);

                var first = results.First();
                foreach (var result in results) {
                    Assert.Equal(first, result);
                }
                Assert.NotEmpty(first);
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }
    }
}
