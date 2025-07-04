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

        [Fact]
        public void ConvertData_MultipleEqualsWithKnownPrefixes_SplitsIntoLines() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 0,
                DataRaw = "v=spf1 include:_spf.example.com ~allgoogle-site-verification=ABC"
            };

            Assert.Equal(
                "v=spf1 include:_spf.example.com ~all\ngoogle-site-verification=ABC",
                answer.Data);
        }

        [Fact]
        public void ConvertData_MultipleEqualsGenericKeyValuePairs_SplitsIntoLines() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TXT,
                TTL = 0,
                DataRaw = "key1=value1key2=value2"
            };

            Assert.Equal("key1=value1\nkey2=value2", answer.Data);
        }
    }
}
