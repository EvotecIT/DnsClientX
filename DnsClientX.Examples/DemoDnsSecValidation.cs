using System.Threading.Tasks;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates performing a DNS query with DNSSEC validation enabled.
    /// </summary>
    internal class DemoDnsSecValidation {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            DnsResponse response = await client.Resolve("evotec.pl", DnsRecordType.DNSKEY, requestDnsSec: true, validateDnsSec: true);
            response.DisplayTable();
        }
    }
}
