using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsMessageOptions.RecursionDesired"/> handling in DNS wire serialization.
    /// </summary>
    public class DnsMessageRecursionDesiredTests {
        private const ushort RdFlag = 0x0100;

        /// <summary>
        /// Ensures the RD bit is cleared when recursion is not desired.
        /// </summary>
        [Fact]
        public void SerializeDnsWireFormat_ShouldClearRdBit_WhenRecursionDesiredFalse() {
            var opts = new DnsMessageOptions(RecursionDesired: false);
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);

            byte[] query = message.SerializeDnsWireFormat();

            ushort flags = ReadFlags(query);
            Assert.True((flags & RdFlag) == 0);
        }

        /// <summary>
        /// Ensures the RD bit is set when recursion is desired.
        /// </summary>
        [Fact]
        public void SerializeDnsWireFormat_ShouldSetRdBit_WhenRecursionDesiredTrue() {
            var opts = new DnsMessageOptions(RecursionDesired: true);
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);

            byte[] query = message.SerializeDnsWireFormat();

            ushort flags = ReadFlags(query);
            Assert.True((flags & RdFlag) != 0);
        }

        private static ushort ReadFlags(byte[] query) {
            if (query == null) {
                throw new ArgumentNullException(nameof(query));
            }
            if (query.Length < 4) {
                throw new ArgumentException("DNS message is too short to contain a header.", nameof(query));
            }

            return (ushort)((query[2] << 8) | query[3]);
        }
    }
}
