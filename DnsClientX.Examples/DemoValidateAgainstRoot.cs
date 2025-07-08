using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates validating DNSSEC data using built-in root trust anchors.
    /// </summary>
    internal static class DemoValidateAgainstRoot {
        /// <summary>Runs the example.</summary>
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            DnsResponse response = await client.Resolve(".", DnsRecordType.DNSKEY, requestDnsSec: true);
            bool valid = DnsSecValidator.ValidateAgainstRoot(response);
            Settings.Logger.WriteInformation($"Response validated against root anchors: {valid}");
        }
    }
}
