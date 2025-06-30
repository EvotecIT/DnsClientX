using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    public class RegexCultureInvariantTests {
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

        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void FilterAnswersRegex_ConsistentAcrossCultures(string culture) {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswersRegex", BindingFlags.NonPublic | BindingFlags.Instance)!;
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
    }
}
