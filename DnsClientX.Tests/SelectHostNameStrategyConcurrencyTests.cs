using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests concurrent access to <see cref="Configuration.SelectHostNameStrategy"/>.
    /// </summary>
    public class SelectHostNameStrategyConcurrencyTests {
        /// <summary>
        /// Ensures host selection remains thread safe under concurrency.
        /// </summary>
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

        /// <summary>
        /// Ensures every query snapshot keeps the selected hostname and URI together.
        /// </summary>
        [Fact]
        public async Task QuerySnapshotsKeepHostnameAndUriConsistent() {
            var config = new Configuration(DnsEndpoint.CloudflareWireFormat, DnsSelectionStrategy.Random);

            Configuration[] snapshots = await Task.WhenAll(Enumerable.Range(0, 500)
                .Select(_ => Task.Run(config.CreateQuerySnapshot)));

            Assert.All(snapshots, snapshot => {
                Assert.NotNull(snapshot.BaseUri);
                Assert.Equal(snapshot.Hostname, snapshot.BaseUri!.Host);
                Assert.Contains(snapshot.Hostname, new[] { "1.1.1.1", "1.0.0.1" });
            });
        }
    }
}
