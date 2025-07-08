using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal class DemoSignedQuery {
        public static async Task Example() {
            using RSA rsa = RSA.Create();
            var client = new ClientXBuilder()
                .WithSigningKey(rsa)
                .Build();

            var response = await client.Resolve("example.com", DnsRecordType.A);
            response?.DisplayTable();
        }
    }
}
