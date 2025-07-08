using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates performing a DNS zone transfer.
    /// </summary>
    internal class DemoZoneTransfer {
        /// <summary>
        /// Executes the zone transfer example.
        /// </summary>
        public static async Task Example() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = 5353 } };
            await foreach (var rrset in client.ZoneTransferStreamAsync("example.com")) {
                Console.WriteLine(string.Join(", ", rrset));
            }
        }
    }
}
