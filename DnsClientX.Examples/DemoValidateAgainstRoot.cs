using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates end-to-end DNSSEC validation using built-in root trust anchors.
    /// </summary>
    internal static class DemoValidateAgainstRoot {
        /// <summary>Runs the example.</summary>
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            DnsResponse response = await client.Resolve("cloudflare.com", DnsRecordType.A, validateDnsSec: true);
            Settings.Logger.WriteInformation($"Response validated against root anchors: {response.DnsSecValidatedLocally}");
        }
    }
}
