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
        public void SelectHostNameStrategy_ShouldSkipUnavailableUntilCooldownExpires() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Failover) {
                UnavailableCooldown = System.TimeSpan.FromMinutes(1)
            };

            config.MarkCurrentHostnameUnavailable();
            config.SelectHostNameStrategy();
            Assert.Equal("1.0.0.1", config.Hostname);

            // Expire the same host through the public mark operation instead of relying on
            // a short wall-clock sleep that may finish before the first assertion on CI.
            config.UnavailableCooldown = System.TimeSpan.Zero;
            config.MarkHostnameUnavailable("1.1.1.1");
            config.AdvanceToNextHostname();
            config.SelectHostNameStrategy();
            Assert.Equal("1.1.1.1", config.Hostname);
        }
    }
}
