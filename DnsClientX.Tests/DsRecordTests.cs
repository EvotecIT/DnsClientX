using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for DS record parsing helpers.
    /// </summary>
    public class DsRecordTests {
        private const string DsString = "20326 8 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D";

        /// <summary>
        /// Verifies that DS record data is parsed correctly from string form.
        /// </summary>
        [Fact]
        public void ConvertData_ParsesDsRecord() {
            var answer = new DnsAnswer {
                Name = "example.com",
                Type = DnsRecordType.DS,
                TTL = 3600,
                DataRaw = DsString
            };

            Assert.Equal("20326 RSASHA256 2 E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D", answer.Data);
        }
    }
}
