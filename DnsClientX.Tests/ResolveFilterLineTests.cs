using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests filtering TXT answers line by line.
    /// </summary>
    public class ResolveFilterLineTests {
        private static DnsAnswer CreateTxt(string data) => new() { Name = "example.com", Type = DnsRecordType.TXT, TTL = 300, DataRaw = data };

        /// <summary>
        /// Ensures substring filtering returns the line containing the text.
        /// </summary>
        [Fact]
        public void FilterAnswers_ReturnsMatchingLine() {
            var client = new ClientX();
            var method = typeof(ClientX).GetMethod("FilterAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateTxt("line1\nline2") };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "line2", DnsRecordType.TXT })!;
            Assert.Single(result);
            Assert.Equal("line2", result[0].Data);
        }

        /// <summary>
        /// Ensures regex filtering returns the line matching the pattern.
        /// </summary>
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
