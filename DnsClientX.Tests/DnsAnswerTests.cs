using System.Globalization;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for helper methods on <see cref="DnsAnswer"/>.
    /// </summary>
    public class DnsAnswerTests {
        /// <summary>
        /// Ensures conversion to a string array returns an empty array when data is null.
        /// </summary>
        [Fact]
        public void ConvertToMultiString_NullData_ReturnsEmptyArray() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 0,
                DataRaw = null!
            };

            Assert.Empty(answer.DataStrings);
        }

        /// <summary>
        /// Ensures that NS record data is parsed consistently regardless of culture.
        /// </summary>
        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void ConvertData_NsRecord_ConsistentAcrossCultures(string culture) {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.NS,
                TTL = 3600,
                DataRaw = "EXAMPLEI.COM"
            };

            var original = Thread.CurrentThread.CurrentCulture;
            try {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                Assert.Equal("examplei.com", answer.Data);
            } finally {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
