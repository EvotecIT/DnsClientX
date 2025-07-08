using System.Linq;
using System.Threading.Tasks;

using DnsClientX.Linq;

namespace DnsClientX.Examples {
    /// <summary>
    /// Demonstrates usage of the LINQ provider.
    /// </summary>
    internal class DemoDnsLinq {
        public static async Task Example() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var names = new[] { "evotec.pl", "google.com" };
            HelpersSpectre.AddLine("DnsQueryable", string.Join(",", names), DnsRecordType.A, DnsEndpoint.Cloudflare);
            var query = client.AsQueryable(names, DnsRecordType.A)
                .Where(a => a.Data.Contains("216.58"));
            var results = await query.ToListAsync();
            results.ToArray().DisplayTable();
        }
    }
}
