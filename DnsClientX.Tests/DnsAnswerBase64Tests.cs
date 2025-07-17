using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests for base64 parsing of <see cref="DnsAnswer"/> data.
    /// </summary>
    public class DnsAnswerBase64Tests {
        /// <summary>
        /// Ensures conversion returns an empty string when no raw data is provided.
        /// </summary>
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
