using System;
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
                dnsResponse.Questions.DisplayTable();
                dnsResponse.Answers.DisplayTable();
            }
        }

        public static async Task ExampleSystemDns() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.System);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.System, DnsSelectionStrategy.Random);
            data.Questions.DisplayTable();
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
    }
}
