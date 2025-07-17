using System.Globalization;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <c>ConvertSpecialFormatToDotted</c> on <see cref="DnsAnswer"/>.
    /// </summary>
    public class ConvertSpecialFormatToDottedTests {
        private static string Invoke(string data) {
            var answer = new DnsAnswer();
            MethodInfo method = typeof(DnsAnswer).GetMethod("ConvertSpecialFormatToDotted", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (string)method.Invoke(answer, new object[] { data })!;
        }

        /// <summary>
        /// If the input cannot be parsed, the original string should be returned.
        /// </summary>
        [Fact]
        public void MalformedInputReturnsOriginal() {
            string malformed = $"{(char)7}examp"; // length byte larger than remaining data
            Assert.Equal(malformed, Invoke(malformed));
        }

        /// <summary>
        /// Standard representation should convert correctly regardless of culture.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void StandardFormat_Converts_CultureInvariant(string culture) {
            const string input = "EXAMPLEI.";
            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Assert.Equal("examplei", Invoke(input));
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
