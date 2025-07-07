using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoServiceDiscovery {
        public static async Task Example() {
            using (var client = new ClientX(DnsEndpoint.Cloudflare)) {
                var results = await client.DiscoverServices("example.com");
                foreach (var r in results) {
                    Console.WriteLine($"{r.ServiceName} -> {r.Target}:{r.Port}");
                }
            }
        }

        /// <summary>
        /// Demonstrates streaming discovery where results are printed as soon as
        /// they are available.
        /// </summary>
        public static async Task ExampleEnumerate() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            await foreach (var r in client.EnumerateServicesAsync("example.com")) {
                Console.WriteLine($"{r.ServiceName} -> {r.Target}:{r.Port}");
            }
        }
    }
}
