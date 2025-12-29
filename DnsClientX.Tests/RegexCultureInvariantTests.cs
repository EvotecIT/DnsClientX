using System.Globalization;
using System.Linq;
using System.Reflection;
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
                Assert.Equal("3 1 1 2b6e0f", answer.Data);
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
        public void FilterAnswersRegex_ConsistentAcrossCultures(string culture) {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "FilterAnswersRegex" && m.GetParameters().Length == 3);
            var answers = new[] {
                new DnsAnswer {
                    Name = "example.com",
                    Type = DnsRecordType.TXT,
                    TTL = 300,
                    DataRaw = "value=test"
                }
            };
            var regex = new Regex("value", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, regex, DnsRecordType.TXT })!;
                Assert.Single(result);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        /// <summary>
        /// Ensures substring filtering yields the same results regardless of culture.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void FilterAnswers_ConsistentAcrossCultures(string culture) {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "FilterAnswers" && m.GetParameters().Length == 3);
            var answers = new[] {
                new DnsAnswer {
                    Name = "example.com",
                    Type = DnsRecordType.CNAME,
                    TTL = 300,
                    DataRaw = "EXAMPLEI.COM"
                }
            };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "i.com", DnsRecordType.CNAME })!;
                Assert.Single(result);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
