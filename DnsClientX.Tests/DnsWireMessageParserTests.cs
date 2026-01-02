using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsWireMessageParser"/> helpers.
    /// </summary>
    public class DnsWireMessageParserTests {
        /// <summary>
        /// Ensures header parsing extracts flags and section counts.
        /// </summary>
        [Fact]
        public void TryParseHeader_ShouldParseFlagsAndCounts() {
            var data = new byte[12];
            // Flags: TC + RD + RA + RCODE=NXDomain
            data[2] = 0x03;
            data[3] = 0x83;
            data[4] = 0x00; data[5] = 0x01; // QD
            data[6] = 0x00; data[7] = 0x02; // AN
            data[8] = 0x00; data[9] = 0x03; // NS
            data[10] = 0x00; data[11] = 0x04; // AR

            bool ok = DnsWireMessageParser.TryParseHeader(data, out var header);

            Assert.True(ok);
            Assert.True(header.IsTruncated);
            Assert.True(header.IsRecursionAvailable);
            Assert.True(header.IsRecursionDesired);
            Assert.Equal(DnsResponseCode.NXDomain, header.ResponseCode);
            Assert.Equal((ushort)1, header.QuestionCount);
            Assert.Equal((ushort)2, header.AnswerCount);
            Assert.Equal((ushort)3, header.AuthorityCount);
            Assert.Equal((ushort)4, header.AdditionalCount);
        }

        /// <summary>
        /// Ensures EDNS parsing finds the OPT record and returns the UDP payload size.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnUdpPayloadSize_WhenOptPresent() {
            // Header (QD=1, AR=1)
            var message = new byte[] {
                0x00, 0x01, // ID
                0x00, 0x00, // Flags
                0x00, 0x01, // QD
                0x00, 0x00, // AN
                0x00, 0x00, // NS
                0x00, 0x01, // AR
                // Question: example.com A IN
                0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
                0x03, (byte)'c', (byte)'o', (byte)'m',
                0x00,
                0x00, 0x01, // QTYPE A
                0x00, 0x01, // QCLASS IN
                // Additional: OPT (root name)
                0x00,
                0x00, 0x29, // TYPE OPT
                0x04, 0xD0, // CLASS: UDP payload size = 1232
                0x00, 0x00, 0x00, 0x00, // TTL
                0x00, 0x00 // RDLEN
            };

            bool ok = DnsWireMessageParser.TryParseEdns(message, out var edns);

            Assert.True(ok);
            Assert.True(edns.Supported);
            Assert.Equal(1232, edns.UdpPayloadSize);
        }

        /// <summary>
        /// Ensures EDNS parsing returns a non-supported result when no OPT record is present.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnUnsupported_WhenNoOptPresent() {
            // Header (QD=1, AR=0)
            var message = new byte[] {
                0x00, 0x01, // ID
                0x00, 0x00, // Flags
                0x00, 0x01, // QD
                0x00, 0x00, // AN
                0x00, 0x00, // NS
                0x00, 0x00, // AR
                // Question: example.com A IN
                0x07, (byte)'e', (byte)'x', (byte)'a', (byte)'m', (byte)'p', (byte)'l', (byte)'e',
                0x03, (byte)'c', (byte)'o', (byte)'m',
                0x00,
                0x00, 0x01, // QTYPE A
                0x00, 0x01 // QCLASS IN
            };

            bool ok = DnsWireMessageParser.TryParseEdns(message, out var edns);

            Assert.True(ok);
            Assert.False(edns.Supported);
            Assert.Equal(0, edns.UdpPayloadSize);
        }
    }
}
