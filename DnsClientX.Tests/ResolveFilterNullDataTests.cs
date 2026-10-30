using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests public filtering when answers contain empty data strings.
    /// </summary>
    public class ResolveFilterNullDataTests {
        private static DnsAnswer CreateAnswer(string dataRaw, DnsRecordType type) {
            return new DnsAnswer {
                Name = "example.com",
                Type = type,
                TTL = 300,
                DataRaw = dataRaw
            };
        }

        /// <summary>
        /// Ensures answers with empty data are ignored by scalar substring filtering.
        /// </summary>
        [Fact]
        public async Task FilterAnswers_ShouldIgnoreEmptyData() {
            using var client = new ClientX();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) }
            });
            var result = (await client.ResolveFilter("example.com", DnsRecordType.A, "test", retryOnTransient: false)).Answers;
            Assert.Empty(result);
        }

        /// <summary>
        /// Ensures answers with empty data are ignored by scalar regex filtering.
        /// </summary>
        [Theory]
        [InlineData("test")]
        [InlineData(".*")]
        public async Task FilterAnswersRegex_ShouldIgnoreEmptyData(string pattern) {
            using var client = new ClientX();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) }
            });
            var result = (await client.ResolveFilter("example.com", DnsRecordType.A, new Regex(pattern, RegexOptions.CultureInvariant), retryOnTransient: false)).Answers;
            Assert.Empty(result);
        }

        /// <summary>
        /// Validates batch substring filtering omits responses when data is empty.
        /// </summary>
        [Theory]
        [InlineData("test")]
        [InlineData("")]
        public async Task HasMatchingAnswers_ShouldReturnFalseForEmptyData(string filter) {
            using var client = new ClientX();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) }
            });
            var result = await client.ResolveFilter(new[] { "example.com" }, DnsRecordType.A, filter, retryOnTransient: false);
            Assert.Empty(result);
        }

        /// <summary>
        /// Validates batch regex filtering omits responses when data is empty.
        /// </summary>
        [Theory]
        [InlineData("test")]
        [InlineData(".*")]
        public async Task HasMatchingAnswersRegex_ShouldReturnFalseForEmptyData(string pattern) {
            using var client = new ClientX();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse {
                Answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) }
            });
            var result = await client.ResolveFilter(new[] { "example.com" }, DnsRecordType.A, new Regex(pattern, RegexOptions.CultureInvariant), retryOnTransient: false);
            Assert.Empty(result);
        }
    }
}
