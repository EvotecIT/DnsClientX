using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Exercises a deterministic corpus of structurally invalid DNS messages.</summary>
    public class DnsWireAdversarialCorpusTests {
        /// <summary>Gets malformed packets and the bounded-parser error each one must produce.</summary>
        public static IEnumerable<object[]> MalformedMessages() {
            yield return new object[] { new byte[11], "12-byte header" };
            yield return new object[] {
                new byte[] { 0, 1, 0x81, 0x80, 0, 1, 0, 0, 0, 0, 0, 0 },
                "boundary"
            };
            yield return new object[] {
                new byte[] { 0, 1, 0x81, 0x80, 0, 1, 0, 0, 0, 0, 0, 0, 0x40 },
                "reserved label"
            };
            yield return new object[] {
                new byte[] { 0, 1, 0x81, 0x80, 0, 0, 0, 1, 0, 0, 0, 0, 0xC0, 0xFF },
                "earlier message offset"
            };
            yield return new object[] {
                new byte[] {
                    0, 1, 0x81, 0x80, 0, 0, 0, 1, 0, 0, 0, 0,
                    0, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2
                },
                "truncated"
            };
            yield return new object[] {
                new byte[] { 0, 1, 0x81, 0x80, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF },
                "trailing bytes"
            };
        }

        /// <summary>Ensures malformed inputs fail explicitly instead of being partly accepted or read out of bounds.</summary>
        [Theory]
        [MemberData(nameof(MalformedMessages))]
        public async Task MalformedMessageIsRejected(byte[] message, string expectedMessage) {
            DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(
                () => DnsWire.DeserializeDnsWireFormat(null, false, message));

            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
