using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates cancelling a resolve operation.
    /// </summary>
    internal class DemoResolveCancellation {
        /// <summary>Runs the demo.</summary>
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            try {
                HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.A, DnsEndpoint.Cloudflare);
                var response = await client.Resolve("github.com", cancellationToken: cts.Token);
                response.DisplayTable();
            } catch (TaskCanceledException) {
                Console.WriteLine("Resolve operation canceled.");
            }
        }
    }
}
