using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoResolveUdpTcp {


        public static async Task ExampleTestingUdp() {
            HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.TXT, "192.168.241.6", DnsRequestFormat.DnsOverUDP);
            ClientX client = new ClientX("192.168.241.6", DnsRequestFormat.DnsOverUDP) {
                Debug = true
            };
            var data = await client.Resolve("github.com", DnsRecordType.TXT);
            data.DisplayTable();
        }

        public static async Task ExampleTestingTcp() {
            HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.TXT, "192.168.241.5", DnsRequestFormat.DnsOverTCP);
            ClientX client = new ClientX("192.168.241.5", DnsRequestFormat.DnsOverTCP) {
                Debug = true
            };
            var data = await client.Resolve("github.com", DnsRecordType.TXT);
            data.DisplayTable();
        }
    }
}
