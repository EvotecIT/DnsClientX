using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveFilterLineTests {
        private static DnsAnswer CreateTxt(string data) => new() { Name = "example.com", Type = DnsRecordType.TXT, TTL = 300, DataRaw = data };

        [Fact]
        public void FilterAnswers_ReturnsMatchingLine() {
            var client = new ClientX();
            var method = typeof(ClientX).GetMethod("FilterAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateTxt("line1\nline2") };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "line2", DnsRecordType.TXT })!;
            Assert.Single(result);
            Assert.Equal("line2", result[0].Data);
        }

        [Fact]
        public void FilterAnswersRegex_ReturnsMatchingLine() {
            var client = new ClientX();
            var method = typeof(ClientX).GetMethod("FilterAnswersRegex", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateTxt("line1\nline2") };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, new Regex("line2$"), DnsRecordType.TXT })!;
            Assert.Single(result);
            Assert.Equal("line2", result[0].Data);
        }
    }
}
