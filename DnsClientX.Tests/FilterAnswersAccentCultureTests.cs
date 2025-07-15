using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    public class FilterAnswersAccentCultureTests {
        private static DnsAnswer CreateTxt(string data) => new() { Name = "example.com", Type = DnsRecordType.TXT, TTL = 300, DataRaw = data };
        private static DnsAnswer CreateCname(string data) => new() { Name = "example.com", Type = DnsRecordType.CNAME, TTL = 300, DataRaw = data };

        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void FilterAnswers_Txt_Accents_AcrossCultures(string culture) {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateTxt("CAF\u00C9\nOTHER") };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "café", DnsRecordType.TXT })!;
                Assert.Single(result);
                Assert.Equal("CAF\u00C9", result[0].Data);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void FilterAnswers_NonTxt_Accents_AcrossCultures(string culture) {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateCname("r\u00E9sum\u00E9.example.com") };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "R\u00C9SUM\u00C9", DnsRecordType.CNAME })!;
                Assert.Single(result);
                Assert.Equal("r\u00E9sum\u00E9.example.com", result[0].Data);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
