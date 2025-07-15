using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for helper methods on <see cref="DnsAnswer"/>.
    /// </summary>
    public class DnsAnswerTests {
        [Fact]
        /// <summary>
        /// Ensures conversion to a string array returns an empty array when data is null.
        /// </summary>
        public void ConvertToMultiString_NullData_ReturnsEmptyArray() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 0,
                DataRaw = null!
            };

            Assert.Empty(answer.DataStrings);
        }
    }
}
