using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    public class DemoByManualUrl {
        public static async Task Example() {
            HelpersSpectre.AddLine("Resolve", "evotec.pl", DnsRecordType.A, "1.1.1.1", dnsRequestFormat: DnsRequestFormat.JSON);
            ClientX client = new ClientX("1.1.1.1", DnsRequestFormat.JSON);
            var data = await client.Resolve("evotec.pl", DnsRecordType.A);
            data.DisplayTable();
        }

        public static async Task Example2() {
            HelpersSpectre.AddLine("Resolve", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            ClientX client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            var data = await client.Resolve("evotec.pl", DnsRecordType.A);
            data.DisplayTable();
        }

        // TODO - This method is not yet working correctly
        public static async Task ExampleTesting() {
            HelpersSpectre.AddLine("Resolve", "www.example.com", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatPost);
            ClientX client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatDot) {
                Debug = true
            };
            var data = await client.Resolve("www.example.com", DnsRecordType.A);
            data.DisplayTable();
        }
    }
}
