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
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, DnsEndpoint.CloudflareWireFormat);
            data.Answers.DisplayToConsole();
        }
        public static async Task Example2() {
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, "1.1.1.1", DnsRequestFormat.JSON);
            data.Answers.DisplayToConsole();
        }

        public static async Task Example3() {
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            data.Answers.DisplayToConsole();
        }

        // TODO - This method is not yet working correctly
        public static async Task ExampleTesting() {
            var data = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatPost);
            data.Answers.DisplayToConsole();
        }
    }
}
