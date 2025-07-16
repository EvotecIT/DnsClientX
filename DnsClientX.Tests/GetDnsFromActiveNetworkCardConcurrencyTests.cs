using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class GetDnsFromActiveNetworkCardConcurrencyTests {
        [Fact]
        public async Task RefreshConcurrentCalls_ShouldReturnConsistentResults() {
            var expected = new List<string> { "1.1.1.1", "8.8.8.8" };
            SystemInformation.SetDnsServerProvider(() => new List<string>(expected));
            try {
                var tasks = Enumerable.Range(0, 20)
                    .Select(_ => Task.Run(() => SystemInformation.GetDnsFromActiveNetworkCard(refresh: true)));
                var results = await Task.WhenAll(tasks);

                foreach (var result in results) {
                    Assert.Equal(expected, result);
                }
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }
    }
}
