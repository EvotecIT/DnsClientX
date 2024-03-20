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
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.JSON);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.JSON);
            data.Answers.DisplayTable();
        }

        public static async Task Example3() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            data.Answers.DisplayTable();
        }

        // TODO - This method is not yet working correctly
        public static async Task ExampleTesting() {
            HelpersSpectre.AddLine("QueryDns", "evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatPost);
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatPost);
            data.Answers.DisplayTable();
        }
    }
}
