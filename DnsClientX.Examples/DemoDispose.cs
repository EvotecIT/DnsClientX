using System.Threading.Tasks;

namespace DnsClientX.Examples {
    internal static class DemoDispose {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var response = await client.ResolveFirst("evotec.pl");
            response?.DisplayTable();
        }
    }
}
