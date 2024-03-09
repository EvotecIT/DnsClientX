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
    }
}
