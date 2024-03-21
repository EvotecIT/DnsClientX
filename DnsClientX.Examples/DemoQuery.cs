using System;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.Examples {
    internal class DemoQuery {
        public static async Task Example0() {
            DnsClientX.DnsQuestion question;
            DnsClientX.DnsAnswer answer;
        }

        public static async Task Example1() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareWireFormat);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareWireFormat);
            data.Answers.DisplayTable();
        }
        public static async Task Example2() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            data.Answers.DisplayTable();
        }

        public static async Task Example3() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);
            data.Answers.DisplayTable();
        }

        public static async Task ExampleHttpsOverPost() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsPOST);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsPOST);
            data.Answers.DisplayTable();
        }
    }
}
