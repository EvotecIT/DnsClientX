using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    public class DemoByManualUrl {
        public static async Task Example() {
            ClientX client = new ClientX("1.1.1.1", DnsRequestFormat.JSON);
            var data = await client.Resolve("evotec.pl", DnsRecordType.A);
            data.Answers.DisplayToConsole();
        }

        public static async Task Example2() {
            ClientX client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.JSON);
            var data = await client.Resolve("evotec.pl", DnsRecordType.A);
            data.Answers.DisplayToConsole();
        }

        // TODO - This method is not yet working correctly
        public static async Task ExampleTesting() {
            ClientX client = new ClientX(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.WireFormatDot) {
                Debug = true
            };
            var data = await client.Resolve("www.example.com", DnsRecordType.A);
            data.Answers.DisplayToConsole();
        }
    }
}
