using System.Collections.Generic;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates resolving DNS queries concurrently using <see cref="ClientX.ResolveAsyncEnumerable"/>.
    /// </summary>
    internal class DemoResolveAsyncEnumerable {
        /// <summary>Runs the demo.</summary>
        public static async Task Example() {
            var names = new[] { "github.com", "microsoft.com" };
            var types = new[] { DnsRecordType.A, DnsRecordType.MX };

            using var client = new ClientX(DnsEndpoint.System);

            await foreach (var response in client.ResolveAsyncEnumerable(names, types, retryOnTransient: false)) {
                response.DisplayToConsole();
            }
        }
    }
}
