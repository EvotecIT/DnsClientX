using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests filtering helpers when answers contain empty data strings.
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
        /// Ensures answers with empty data are ignored by <c>FilterAnswers</c>.
        /// </summary>
        [Fact]
        public void FilterAnswers_ShouldIgnoreEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "test", DnsRecordType.A })!;
            Assert.Empty(result);
        }

        /// <summary>
        /// Ensures answers with empty data are ignored by <c>FilterAnswersRegex</c>.
        /// </summary>
        [Fact]
        public void FilterAnswersRegex_ShouldIgnoreEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswersRegex", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, new Regex("test", RegexOptions.CultureInvariant), DnsRecordType.A })!;
            Assert.Empty(result);
        }

        /// <summary>
        /// Validates <c>HasMatchingAnswers</c> returns false when data is empty.
        /// </summary>
        [Fact]
        public void HasMatchingAnswers_ShouldReturnFalseForEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("HasMatchingAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (bool)method.Invoke(client, new object[] { answers, "test", DnsRecordType.A })!;
            Assert.False(result);
        }

        /// <summary>
        /// Validates <c>HasMatchingAnswersRegex</c> returns false when data is empty.
        /// </summary>
        [Fact]
        public void HasMatchingAnswersRegex_ShouldReturnFalseForEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("HasMatchingAnswersRegex", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (bool)method.Invoke(client, new object[] { answers, new Regex("test", RegexOptions.CultureInvariant), DnsRecordType.A })!;
            Assert.False(result);
        }
    }
}
