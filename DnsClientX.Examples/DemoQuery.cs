using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using DnsClientX;

namespace DnsClientX.Examples {
    internal class DemoQuery {
        public static async Task Example0() {
            var domains = new[] { "evotec.pl", "google.com" };
            HelpersSpectre.AddLine("QueryDns", "evotec.pl / google.com", DnsRecordType.A, "1.1.1.1");
            var data = await ClientX.QueryDns(domains, DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            data.DisplayTable();
        }

        public static async Task ExamplePTR() {
            var domains = new[] { "1.1.1.1" };
            HelpersSpectre.AddLine("QueryDns", "1.1.1.1", DnsRecordType.A, "1.1.1.1");
            var data = await ClientX.QueryDns(domains, DnsRecordType.PTR, "1.1.1.1", DnsRequestFormat.DnsOverHttps);
            data.DisplayTable();
        }

        public static async Task ExamplePTR1() {
            var domains = new[] { "1.1.1.1", "192.168.241.5", "192.168.241.108" };
            HelpersSpectre.AddLine("QueryDns", "1.1.1.1", DnsRecordType.A, "192.168.241.5");
            var data = await ClientX.QueryDns(domains, DnsRecordType.PTR, "192.168.241.5", DnsRequestFormat.DnsOverUDP);
            data.DisplayTable();
        }

        public static async Task ExamplePTR2() {
            var domains = new[] { "1.1.1.1", "192.168.241.5", "192.168.241.108" };
            HelpersSpectre.AddLine("QueryDns", "1.1.1.1", DnsRecordType.A, "192.168.241.5");
            var data = await ClientX.QueryDns(domains, DnsRecordType.PTR, "192.168.241.5", DnsRequestFormat.DnsOverTCP);
            data.DisplayTable();
        }

        public static async Task ExamplePTR3() {
            var domains = new[] { "1.1.1.1", "108.138.7.68" };
            HelpersSpectre.AddLine("QueryDns", "1.1.1.1", DnsRecordType.A, "1.1.1.1");
            var data = await ClientX.QueryDns(domains, DnsRecordType.PTR, "1.1.1.1", DnsRequestFormat.DnsOverHttps);
            data.DisplayTable();
        }


        public static async Task Example1() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareWireFormat);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareWireFormat);
            data.Answers.DisplayTable();
        }

        public static async Task Example2() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, "1.1.1.1",
                DnsRequestFormat.DnsOverHttpsJSON);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, "1.1.1.1",
                DnsRequestFormat.DnsOverHttpsJSON);
            data.Answers.DisplayTable();
        }

        public static async Task Example3() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);
            data.Answers.DisplayTable();
        }

        public static async Task ExampleHttpsOverPost() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"),
                DnsRequestFormat.DnsOverHttpsPOST);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"),
                DnsRequestFormat.DnsOverHttpsPOST);
            data.Answers.DisplayTable();
        }

        public static async Task ExampleJsonPost() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"),
                DnsRequestFormat.DnsOverHttpsJSONPOST);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"),
                DnsRequestFormat.DnsOverHttpsJSONPOST);
            data.Answers.DisplayTable();
        }

        public static async Task ExampleGoogleOverWire() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.GoogleWireFormat);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.GoogleWireFormat);
            data.Answers.DisplayTable();
        }

        public static async Task ExampleGoogleOverWirePost() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.GoogleWireFormatPost);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.GoogleWireFormatPost);
            data.Answers.DisplayTable();
        }

        public static async Task ExampleCloudflareSelection() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.Cloudflare);
            var data = await ClientX.QueryDns(["evotec.pl", "google.com", "onet.pl"], DnsRecordType.A, DnsEndpoint.Cloudflare, DnsSelectionStrategy.Random);
            foreach (var dnsResponse in data) {
                dnsResponse.Questions?.DisplayTable();
                dnsResponse.Answers.DisplayTable();
            }
        }

        public static async Task ExampleSystemDns() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.System);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.System, DnsSelectionStrategy.Random);
            data.Questions?.DisplayTable();
            data.Answers.DisplayTable();
        }

        public static async Task ExampleTLSA() {
            var domains = "_25._tcp.mail.ietf.org";
            foreach (DnsEndpoint endpoint in Enum.GetValues(typeof(DnsEndpoint))) {
                HelpersSpectre.AddLine("QueryDns", domains, DnsRecordType.TLSA, endpoint);
                var data = await ClientX.QueryDns(domains, DnsRecordType.TLSA, endpoint);
                data.DisplayTable();
            }
        }

        public static async Task ExampleDS() {
            const string domain = "evotec.pl";
            foreach (DnsEndpoint endpoint in Enum.GetValues(typeof(DnsEndpoint))) {
                HelpersSpectre.AddLine("QueryDns", domain, DnsRecordType.DS, endpoint);
                var data = await ClientX.QueryDns(domain, DnsRecordType.DS, endpoint);
                data.DisplayTable();
            }
        }

        public static async Task ExampleTXTAll() {
            var domains = new[] { "disneyplus.com" };
            foreach (DnsEndpoint endpoint in Enum.GetValues(typeof(DnsEndpoint))) {
                HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.TXT, endpoint);
                var data = await ClientX.QueryDns(domains, DnsRecordType.TXT, endpoint);
                foreach (var d in data[0].Answers) {
                    Settings.Logger.WriteInformation(d.Data);
                }
            }
        }

        public static async Task ExampleTXT() {
            var domains = new[] { "disneyplus.com" };
            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.TXT, "1.1.1.1");
            var data = await ClientX.QueryDns(domains, DnsRecordType.TXT, "1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            foreach (var d in data[0].Answers) {
                Settings.Logger.WriteInformation(d.Data);
            }


            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.TXT, DnsEndpoint.GoogleWireFormat);
            var dataGoogle = await ClientX.QueryDns(domains, DnsRecordType.TXT, DnsEndpoint.GoogleWireFormat);
            foreach (var d in dataGoogle[0].Answers) {
                Settings.Logger.WriteInformation(d.Data);
            }
        }

        public static async Task ExampleTXTQuad() {
            var domains = new[] { "disneyplus.com" };
            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.TXT, "1.1.1.1");
            var data = await ClientX.QueryDns(domains, DnsRecordType.TXT, "1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            foreach (var d in data[0].Answers) {
                Settings.Logger.WriteInformation(d.Data);
            }


            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.TXT, DnsEndpoint.Quad9);
            var dataGoogle = await ClientX.QueryDns(domains, DnsRecordType.TXT, DnsEndpoint.Quad9);
            foreach (var d in dataGoogle[0].Answers) {
                Settings.Logger.WriteInformation(d.Data);
            }
        }

        public static async Task ExampleSPF() {
            var domains = new[] { "disneyplus.com" };
            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.SPF, "1.1.1.1");
            using (var client = new ClientX(DnsEndpoint.Cloudflare, DnsSelectionStrategy.First) {
                   Debug = false
               }) {
                var data = await client.ResolveFilter("disneyplus.com", DnsRecordType.TXT, "SPF1");
                Settings.Logger.WriteInformation(data.Answers[0].Data);
            }

            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.SPF, DnsEndpoint.Google);
            using (var client1 = new ClientX(DnsEndpoint.GoogleWireFormat, DnsSelectionStrategy.First) {
                   Debug = false
               }) {
                var data1 = await client1.ResolveFilter("disneyplus.com", DnsRecordType.TXT, "SPF1");
                Settings.Logger.WriteInformation(data1.Answers[0].Data);
            }
        }

        public static async Task ExampleSPFQuad() {
            var domains = new[] { "disneyplus.com" };
            HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.SPF, "1.1.1.1");
            using (var client = new ClientX(DnsEndpoint.Cloudflare, DnsSelectionStrategy.First) {
                   Debug = false
               }) {
                var data = await client.ResolveFilter("disneyplus.com", DnsRecordType.TXT, "SPF1");
                Settings.Logger.WriteInformation(data.Answers[0].Data);
            }

            foreach (DnsEndpoint endpoint in Enum.GetValues(typeof(DnsEndpoint))) {
                HelpersSpectre.AddLine("QueryDns", "disneyplus.com", DnsRecordType.SPF, endpoint);
                using (var client1 = new ClientX(endpoint, DnsSelectionStrategy.First) {
                       Debug = false
                   }) {
                    var data1 = await client1.ResolveFilter("disneyplus.com", DnsRecordType.TXT, "SPF1");
                    Settings.Logger.WriteInformation(data1.Answers[0].Data);
                    Settings.Logger.WriteInformation(data1.Answers[0].DataStrings.Length.ToString());
                    Settings.Logger.WriteInformation(data1.Answers[0].DataStrings[0]);
                }
            }
        }
    }
}
