using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the handling of unavailable hostname cooldowns.
    /// </summary>
    public class UnavailableCooldownTests {
        /// <summary>
        /// Verifies that host selection skips unavailable hosts until cooldown expires.
        /// </summary>
        [Fact]
        public async Task SelectHostNameStrategy_ShouldSkipUnavailableUntilCooldownExpires() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Failover) {
                UnavailableCooldown = System.TimeSpan.FromMilliseconds(100)
            };

            config.MarkCurrentHostnameUnavailable();
            config.SelectHostNameStrategy();
            Assert.Equal("1.0.0.1", config.Hostname);

            await Task.Delay(150);

            typeof(Configuration).GetMethod("AdvanceToNextHostname", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(config, null);
            config.SelectHostNameStrategy();
            Assert.Equal("1.1.1.1", config.Hostname);
        }
    }
}
