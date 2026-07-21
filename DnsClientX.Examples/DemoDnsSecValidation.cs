using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates performing a DNS query with DNSSEC validation enabled.
    /// </summary>
    internal class DemoDnsSecValidation {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            DnsResponse response = await client.Resolve("cloudflare.com", DnsRecordType.A, validateDnsSec: true);
            Settings.Logger.WriteInformation($"Local DNSSEC status: {response.DnsSecValidationStatus} ({response.DnsSecValidationMessage})");
            response.DisplayTable();
        }
    }
}
