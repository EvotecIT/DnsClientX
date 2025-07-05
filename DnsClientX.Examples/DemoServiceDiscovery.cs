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
    }
}
