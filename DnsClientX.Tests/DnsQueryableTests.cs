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
        [Fact]
        public async Task ShouldFilterResults_WithResolverOverride() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            // Avoid network: provide deterministic answers
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = name, Type = type, DataRaw = "1.1.1.1" },
                    new DnsAnswer { Name = name, Type = type, DataRaw = "" }
                }
            });

            var query = client.AsQueryable(new[] { "example.com" }, DnsRecordType.A)
                .Where(a => a.Data.Length > 0);

            var results = await query.ToListAsync();
            Assert.Single(results);
            Assert.Equal("1.1.1.1", results[0].Data);
        }
    }
}
