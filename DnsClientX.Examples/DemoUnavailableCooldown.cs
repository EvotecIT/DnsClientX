using System;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates skipping endpoints marked as unavailable for a cooldown period.
    /// </summary>
    internal static class DemoUnavailableCooldown {
        public static void Example() {
            var config = new Configuration(DnsEndpoint.Cloudflare, DnsSelectionStrategy.Failover) {
                UnavailableCooldown = TimeSpan.FromSeconds(30)
            };

            config.MarkCurrentHostnameUnavailable();
            config.SelectHostNameStrategy();
            Console.WriteLine($"Using {config.Hostname}");
        }
    }
}
