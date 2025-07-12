using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates accessing the retry count from a DNS response.
    /// </summary>
    internal static class DemoRetryCount {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.Resolve("example.com");
            Console.WriteLine($"Retries used: {response.RetryCount}");
        }
    }
}
