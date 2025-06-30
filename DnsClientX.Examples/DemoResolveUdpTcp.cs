using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoResolveUdpTcp {
        public static async Task ExampleTestingUdp() {
            HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.TXT, "192.168.241.6", DnsRequestFormat.DnsOverUDP);
            using var client = new ClientX("192.168.241.6", DnsRequestFormat.DnsOverUDP) {
                Debug = true
            };
            var data = await client.Resolve("github.com", DnsRecordType.TXT).ConfigureAwait(false);
            data.DisplayTable();
        }

        public static async Task ExampleTestingTcp() {
            HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.TXT, "192.168.241.5", DnsRequestFormat.DnsOverTCP);
            using var client = new ClientX("192.168.241.5", DnsRequestFormat.DnsOverTCP) {
                Debug = true
            };
            var data = await client.Resolve("github.com", DnsRecordType.TXT).ConfigureAwait(false);
            data.DisplayTable();
        }

        public static async Task ExampleTestingUdpWrongServer() {
            HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.TXT, "8.8.1.1", DnsRequestFormat.DnsOverUDP);
            using var client = new ClientX("8.8.1.1", DnsRequestFormat.DnsOverUDP) {
                Debug = true
            };
            var data = await client.Resolve("github.com", DnsRecordType.TXT).ConfigureAwait(false);
            data.DisplayTable();
        }

        public static async Task ExampleTestingUdpWrongServer1() {
            HelpersSpectre.AddLine("Resolve", "github.com", DnsRecordType.TXT, "a1-226.akam.net", DnsRequestFormat.DnsOverUDP);
            using var client = new ClientX("a1akam1.net", DnsRequestFormat.DnsOverUDP) {
                Debug = true
            };
            var data = await client.Resolve("github.com", DnsRecordType.TXT).ConfigureAwait(false);
            data.DisplayTable();
        }
    }
}
