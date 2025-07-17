using System;
using System.Text;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for parsing various DNS answer formats.
    /// </summary>
    public class DnsAnswerParsingTests {
        private static byte[] PtrBytes => new byte[] {
            3, (byte)'w', (byte)'w', (byte)'w',
            6, (byte)'g', (byte)'o', (byte)'o', (byte)'g', (byte)'l', (byte)'e',
            3, (byte)'c', (byte)'o', (byte)'m',
            0
        };

        /// <summary>
        /// Validates that PTR records encoded as a DNS name are parsed correctly.
        /// </summary>
        [Fact]
        public void PtrDataFromSpecialFormatIsConverted() {
            var answer = new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = Encoding.UTF8.GetString(PtrBytes) };
            Assert.Equal("www.google.com", answer.Data);
        }

        /// <summary>
        /// Ensures that base64-encoded PTR data is converted properly.
        /// </summary>
        [Fact]
        public void PtrDataFromBase64IsConverted() {
            var base64 = Convert.ToBase64String(PtrBytes);
            var answer = new DnsAnswer { Type = DnsRecordType.PTR, DataRaw = base64 };
            Assert.Equal("www.google.com", answer.Data);
        }

        /// <summary>
        /// Tests parsing of base64 encoded NAPTR record data.
        /// </summary>
        [Fact]
        public void NaptrDataFromBase64IsParsed() {
            byte[] rdata = {
                0x00, 0x01, // order = 1
                0x00, 0x02, // preference = 2
                0x01, (byte)'u', // flags "u"
                0x03, (byte)'s', (byte)'i', (byte)'p', // service "sip"
                0x00, // regexp length 0
                0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
                0x03, (byte)'c', (byte)'o', (byte)'m',
                0x00 // terminator
            };
            var base64 = Convert.ToBase64String(rdata);
            var answer = new DnsAnswer { Type = DnsRecordType.NAPTR, DataRaw = base64 };

            Assert.Equal("1 2 \"u\" \"sip\" \"\" example.com", answer.Data);
        }
    }
}

