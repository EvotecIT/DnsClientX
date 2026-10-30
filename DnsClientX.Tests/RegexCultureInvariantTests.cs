using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests that regex-based filtering behaves consistently across cultures.
    /// </summary>
    public class RegexCultureInvariantTests {
        /// <summary>
        /// Verifies <see cref="DnsAnswer.Data"/> conversion is not affected by culture.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void ConvertData_TlsaRecord_ConsistentAcrossCultures(string culture) {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TLSA,
                TTL = 3600,
                DataRaw = "3 1 1 2b6e0f"
            };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Assert.Equal("3 1 1 2B6E0F", answer.Data);
                Assert.Equal("3 1 1 2b6e0f", answer.DataRaw);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>
        /// Ensures regex filtering yields the same results regardless of culture.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public async Task FilterAnswersRegex_ConsistentAcrossCultures(string culture) {
            using var client = new ClientX();
            var answers = new[] {
                new DnsAnswer {
                    Name = "example.com",
                    Type = DnsRecordType.TXT,
                    TTL = 300,
                    DataRaw = "value=test"
                }
            };
            answers = answers.Concat(new[] {
                new DnsAnswer { Name = "example.com", Type = DnsRecordType.TXT, TTL = 300, DataRaw = "VALUE=I" }
            }).ToArray();
            client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse { Answers = answers });
            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                var regex = new Regex("value=(test|i)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var result = (await client.ResolveFilter("example.com", DnsRecordType.TXT, regex, retryOnTransient: false)).Answers;
                Assert.Equal(new[] { "value=test", "VALUE=I" }, result.Select(answer => answer.DataRaw));
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>
        /// Ensures substring filtering yields the same results regardless of culture.
        /// </summary>
        [Theory]
        [InlineData("en-US", DnsRecordType.CNAME, "examplei.com")]
        [InlineData("tr-TR", DnsRecordType.CNAME, "examplei.com")]
        [InlineData("en-US", DnsRecordType.TXT, "EXAMPLEI.COM")]
        [InlineData("tr-TR", DnsRecordType.TXT, "EXAMPLEI.COM")]
        public async Task FilterAnswers_ConsistentAcrossCultures(string culture, DnsRecordType type, string expectedData) {
            using var client = new ClientX();
            var answers = new[] {
                new DnsAnswer {
                    Name = "example.com",
                    Type = type,
                    TTL = 300,
                    DataRaw = "EXAMPLEI.COM"
                }
            };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                client.ResolverOverride = (name, type, ct) => Task.FromResult(new DnsResponse { Answers = answers });
                var result = (await client.ResolveFilter("example.com", type, "i.com", retryOnTransient: false)).Answers;
                var answer = Assert.Single(result);
                Assert.Equal(expectedData, answer.Data);
                Assert.Equal("EXAMPLEI.COM", answer.DataRaw);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
