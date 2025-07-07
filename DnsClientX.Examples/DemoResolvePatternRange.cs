using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates numeric range expansion with <see cref="ClientX.ResolvePattern"/>.
    /// </summary>
    internal class DemoResolvePatternRange {
        /// <summary>Runs the demo.</summary>
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var responses = await client.ResolvePattern("web{01..03}.example.com", DnsRecordType.A);
            responses.DisplayTable();
        }
    }
}
