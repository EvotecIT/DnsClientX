using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests filtering TXT resource records containing line characters.
    /// </summary>
    public class ResolveFilterLineTests {
        private static DnsAnswer CreateTxt(string data) => new() { Name = "example.com", Type = DnsRecordType.TXT, TTL = 300, DataRaw = data };

        /// <summary>
        /// Ensures substring filtering preserves the complete matching resource record.
        /// </summary>
        [Fact]
        public async Task FilterAnswers_ReturnsMatchingLine() {
            using var client = new ClientX();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] { CreateTxt("line1\nline2") }
            });
            var result = (await client.ResolveFilter("example.com", DnsRecordType.TXT, "line2", retryOnTransient: false)).Answers;
            Assert.Single(result);
            Assert.Equal("line1\nline2", result[0].Data);
        }

        /// <summary>
        /// Ensures regex filtering preserves the complete matching resource record.
        /// </summary>
        [Fact]
        public async Task FilterAnswersRegex_ReturnsMatchingLine() {
            using var client = new ClientX();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] { CreateTxt("line1\nline2") }
            });
            var result = (await client.ResolveFilter("example.com", DnsRecordType.TXT, new Regex("line2$"), retryOnTransient: false)).Answers;
            Assert.Single(result);
            Assert.Equal("line1\nline2", result[0].Data);
        }
    }
}
