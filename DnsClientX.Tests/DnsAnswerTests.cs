using Xunit;

namespace DnsClientX.Tests {
    public class DnsAnswerTests {
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
    }
}
