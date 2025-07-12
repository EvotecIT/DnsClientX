using System;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoBuilder {
        public static async Task Example() {
            using var client = new ClientXBuilder()
                .WithHostname("1.1.1.1", DnsRequestFormat.DnsOverHttps)
                .WithSelectionStrategy(DnsSelectionStrategy.First)
                .WithTimeout(1500)
                .WithUserAgent("Example/1.0")
                .Build();

            var response = await client.Resolve("example.com", DnsRecordType.A);
            response?.DisplayTable();
        }
    }
}