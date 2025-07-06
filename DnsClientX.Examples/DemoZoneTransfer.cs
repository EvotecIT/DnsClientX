using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoZoneTransfer {
        public static async Task Example() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = 5353 } };
            var records = await client.ZoneTransferAsync("example.com");
            foreach (var rrset in records) {
                Console.WriteLine(string.Join(", ", rrset));
            }
        }
    }
}
