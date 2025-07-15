using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests related to DMARC TXT record handling.
    /// </summary>
    public class DmarcTxtRecordTests {
        [Fact]
        /// <summary>
        /// Ensures that DMARC TXT records remain in a single line when parsed.
        /// </summary>
        public void DmarcRecord_IsNotSplitIntoMultipleLines() {
            string record = "v=DMARC1; p=reject; rua=mailto:report@dmarc-reports.example.net; adkim=s; aspf=s";
            var answer = new DnsAnswer {
                Name = "_dmarc.example.com",
                Type = DnsRecordType.TXT,
                TTL = 60,
                DataRaw = record
            };

            Assert.Equal(record, answer.Data);
        }
    }
}
