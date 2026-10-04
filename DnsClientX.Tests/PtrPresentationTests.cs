using System.Globalization;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the public PTR presentation projection on <see cref="DnsAnswer"/>.
    /// </summary>
    public class PtrPresentationTests {

        /// <summary>
        /// If the input cannot be parsed, the original string should be returned.
        /// </summary>
        [Fact]
        public void MalformedInputReturnsOriginal() {
            string malformed = $"{(char)7}examp"; // length byte larger than remaining data
            Assert.Equal(malformed, new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = malformed }.Data);
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
                Assert.Equal("examplei", new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = input }.Data);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
