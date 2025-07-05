using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class ConvertSpecialFormatToDottedTests {
        private static string Invoke(string data) {
            var answer = new DnsAnswer();
            MethodInfo method = typeof(DnsAnswer).GetMethod("ConvertSpecialFormatToDotted", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (string)method.Invoke(answer, new object[] { data })!;
        }

        [Fact]
        public void MalformedInputReturnsOriginal() {
            string malformed = $"{(char)7}examp"; // length byte larger than remaining data
            Assert.Equal(malformed, Invoke(malformed));
        }
    }
}
