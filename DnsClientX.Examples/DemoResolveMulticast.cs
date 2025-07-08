using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoResolveMulticast {
        public static async Task Example() {
            HelpersSpectre.AddLine("Resolve", "example.local", DnsRecordType.A, "224.0.0.251", DnsRequestFormat.Multicast);
            using var client = new ClientX("224.0.0.251", DnsRequestFormat.Multicast) {
                EndpointConfiguration = { Port = 5353 },
                Debug = true
            };
            var response = await client.Resolve("example.local", DnsRecordType.A);
            response.DisplayTable();
        }
    }
}
