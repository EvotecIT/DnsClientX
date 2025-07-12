using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates streaming a DNS zone transfer using <see cref="ClientX.ZoneTransferStreamAsync"/>.
    /// </summary>
    internal class DemoZoneTransferStream {
        /// <summary>
        /// Executes the streaming zone transfer example.
        /// </summary>
        public static async Task Example() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = 5353 } };
            await foreach (var rrset in client.ZoneTransferStreamAsync("example.com")) {
                Console.WriteLine($"Chunk {rrset.Index} (opening: {rrset.IsOpening}, closing: {rrset.IsClosing}): {string.Join(", ", rrset.Records)}");
            }
        }
    }
}
