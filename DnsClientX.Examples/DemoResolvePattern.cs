using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates resolving multiple hostnames generated from a pattern.
    /// </summary>
    internal class DemoResolvePattern {
        /// <summary>Runs the demo.</summary>
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var responses = await client.ResolvePattern("server[1-3].example.com", DnsRecordType.A);
            responses.DisplayTable();
        }
    }
}
