using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveFilterNullDataTests {
        private static DnsAnswer CreateAnswer(string dataRaw, DnsRecordType type) {
            return new DnsAnswer {
                Name = "example.com",
                Type = type,
                TTL = 300,
                DataRaw = dataRaw
            };
        }

        [Fact]
        public void FilterAnswers_ShouldIgnoreEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, "test", DnsRecordType.A })!;
            Assert.Empty(result);
        }

        [Fact]
        public void FilterAnswersRegex_ShouldIgnoreEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("FilterAnswersRegex", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (DnsAnswer[])method.Invoke(client, new object[] { answers, new Regex("test"), DnsRecordType.A })!;
            Assert.Empty(result);
        }

        [Fact]
        public void HasMatchingAnswers_ShouldReturnFalseForEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("HasMatchingAnswers", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (bool)method.Invoke(client, new object[] { answers, "test", DnsRecordType.A })!;
            Assert.False(result);
        }

        [Fact]
        public void HasMatchingAnswersRegex_ShouldReturnFalseForEmptyData() {
            var client = new ClientX();
            MethodInfo method = typeof(ClientX).GetMethod("HasMatchingAnswersRegex", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var answers = new[] { CreateAnswer(string.Empty, DnsRecordType.A) };
            var result = (bool)method.Invoke(client, new object[] { answers, new Regex("test"), DnsRecordType.A })!;
            Assert.False(result);
        }
    }
}
