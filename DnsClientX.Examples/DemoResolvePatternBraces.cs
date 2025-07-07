using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates brace expansion with <see cref="ClientX.ResolvePattern"/>.
    /// </summary>
    internal class DemoResolvePatternBraces {
        /// <summary>Runs the demo.</summary>
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var responses = await client.ResolvePattern("host{a,b}.example.com", DnsRecordType.A);
            responses.DisplayTable();
        }
    }
}
