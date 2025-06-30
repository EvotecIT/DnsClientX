using Xunit;

namespace DnsClientX.Tests {
    public class DnsAnswerBase64Tests {
        [Fact]
        public void ConvertData_TlsaEmptyDataRaw_ReturnsEmpty() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.TLSA,
                TTL = 3600,
                DataRaw = string.Empty
            };

            Assert.Equal(string.Empty, answer.Data);
        }
    }
}
