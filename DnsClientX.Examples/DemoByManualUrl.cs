using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Shows different ways of creating a <see cref="ClientX"/> instance by
    /// manually specifying the DNS server URL.
    /// </summary>
    public class DemoByManualUrl {
        /// <summary>
        /// Queries <c>evotec.pl</c> using a string based DoH endpoint.
        /// </summary>
        public static async Task Example() {
            HelpersSpectre.AddLine("Resolve", "evotec.pl", DnsRecordType.A, "1.1.1.1", dnsRequestFormat: DnsRequestFormat.DnsOverHttpsJSON);
            using (var client = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON)) {
                var data = await client.Resolve("evotec.pl", DnsRecordType.A);
                data.DisplayTable();
            }
        }

        /// <summary>
        /// Queries <c>evotec.pl</c> using a <see cref="Uri"/> DoH endpoint.
        /// </summary>
        public static async Task Example2() {
            HelpersSpectre.AddLine("Resolve", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);
            using (var client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON)) {
                var data = await client.Resolve("evotec.pl", DnsRecordType.A);
                data.DisplayTable();
            }
        }

        /// <summary>
        /// Queries <c>evotec.pl</c> using Google's DoH endpoint.
        /// </summary>
        public static async Task ExampleGoogle() {
            HelpersSpectre.AddLine("Resolve", "evotec.pl", DnsRecordType.A, new Uri("https://8.8.8.8/resolve"), DnsRequestFormat.DnsOverHttpsJSON);
            using (var client = new ClientX(new Uri("https://8.8.8.8/resolve"), DnsRequestFormat.DnsOverHttpsJSON)) {
                var data = await client.Resolve("evotec.pl", DnsRecordType.A);
                data.DisplayTable();
            }
        }


        /// <summary>
        /// Demonstrates querying over DNS over TLS.
        /// </summary>
        public static async Task ExampleTesting() {
            HelpersSpectre.AddLine("Resolve", "www.example.com", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverTLS);
            using (var client = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverTLS) {
                   Debug = false
               }) {
                var data = await client.Resolve("www.example.com", DnsRecordType.A);
                data.DisplayTable();
            }
        }

        /// <summary>
        /// Demonstrates querying using HTTP POST.
        /// </summary>
        public static async Task ExampleTestingHttpOverPost() {
            HelpersSpectre.AddLine("Resolve", "www.example.com", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsPOST);
            using (var client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsPOST) {
                   Debug = false
               }) {
                var data = await client.Resolve("www.example.com", DnsRecordType.A);
                data.DisplayTable();
            }
        }

        /// <summary>
        /// Demonstrates querying using wire format over HTTP POST.
        /// </summary>
        public static async Task ExampleTestingWirePost() {
            HelpersSpectre.AddLine("Resolve", "www.example.com", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsWirePost);
            using var client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsWirePost) { Debug = false };
            var data = await client.Resolve("www.example.com", DnsRecordType.A);
            data.DisplayTable();
        }

        /// <summary>
        /// Demonstrates querying via UDP transport.
        /// </summary>
        public static async Task ExampleTestingUdp() {
            HelpersSpectre.AddLine("Resolve", "www.example.com", DnsRecordType.A, "192.168.241.6", DnsRequestFormat.DnsOverUDP);
            using (var client = new ClientX("192.168.241.6", DnsRequestFormat.DnsOverUDP) {
                   Debug = false
               }) {
                var data = await client.Resolve("www.example.com", DnsRecordType.A);
                //var data = await DnsWireResolveUdp.ResolveWireFormatUdp("www.example.com", DnsRecordType.A, false, false, true);
                data.DisplayTable();
            }
        }

        /// <summary>
        /// Demonstrates querying via TCP transport.
        /// </summary>
        public static async Task ExampleTestingTcp() {
            HelpersSpectre.AddLine("Resolve", "www.example.com", DnsRecordType.A, "192.168.241.5", DnsRequestFormat.DnsOverTCP);
            using (var client = new ClientX("192.168.241.5", DnsRequestFormat.DnsOverTCP) {
                   Debug = false
               }) {
                var data = await client.Resolve("www.example.com", DnsRecordType.A);
                //var data = await DnsWireResolveUdp.ResolveWireFormatTcp("www.example.com", DnsRecordType.A, false, false, true);
                data.DisplayTable();
            }
        }
    }
}
