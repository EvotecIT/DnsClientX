using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class SelectHostNameStrategyConcurrencyTests {
        [Fact]
        public async Task ShouldHandleConcurrentHostSelection() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Random);

            var tasks = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => {
                    for (int i = 0; i < 50; i++) {
                        config.SelectHostNameStrategy();
                        Assert.Contains(config.Hostname, new[] { "1.1.1.1", "1.0.0.1" });
                    }
                }));

            await Task.WhenAll(tasks);
        }
    }
}
