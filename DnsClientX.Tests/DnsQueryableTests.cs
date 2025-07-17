using System.Linq;
using System.Threading.Tasks;
using DnsClientX.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for LINQ-based DNS querying APIs.
    /// </summary>
    public class DnsQueryableTests {
        /// <summary>
        /// Demonstrates filtering query results via LINQ.
        /// </summary>
        [Fact(Skip = "External dependency - network unreachable in CI")]
        public async Task ShouldFilterResults() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var query = client.AsQueryable(new[] { "evotec.pl" }, DnsRecordType.A)
                .Where(a => a.Data.Length > 0);
            var results = await query.ToListAsync();
            Assert.NotEmpty(results);
        }
    }
}
