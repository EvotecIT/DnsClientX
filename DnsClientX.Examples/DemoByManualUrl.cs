using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    public class DemoByManualUrl {
        public static async Task Example() {
            DnsClientX client = new DnsClientX("1.1.1.1", DnsRequestFormat.JSON);
            var data = await client.Resolve("evotec.pl", DnsRecordType.A);
            data.Answers.DisplayToConsole();
        }

        public static async Task Example2() {
            DnsClientX client = new DnsClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            var data = await client.Resolve("evotec.pl", DnsRecordType.A);
            data.Answers.DisplayToConsole();
        }

        // TODO - This method is not yet working correctly
        public static async Task ExampleTesting() {
            DnsClientX client = new DnsClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatDot) {
                Debug = true
            };
            var data = await client.Resolve("www.example.com", DnsRecordType.A);
            data.Answers.DisplayToConsole();
        }
    }
}
