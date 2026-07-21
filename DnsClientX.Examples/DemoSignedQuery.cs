using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoSignedQuery {
        public static async Task Example() {
            var key = TsigKey.FromBase64("update-key.example.com", "AQIDBAUGBwg=");
            using var client = new ClientXBuilder()
                .WithHostname("127.0.0.1", DnsRequestFormat.DnsOverTCP)
                .WithTsigKey(key)
                .Build();

            DnsResponse response = await client.UpdateRecordAsync(
                "example.com", "www.example.com", DnsRecordType.A, "192.0.2.10", 300);
            response.DisplayTable();
        }
    }
}
