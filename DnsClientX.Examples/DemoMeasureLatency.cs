using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoMeasureLatency {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var latency = await client.MeasureLatencyAsync();
            Console.WriteLine($"Latency: {latency.TotalMilliseconds} ms");
        }
    }
}
