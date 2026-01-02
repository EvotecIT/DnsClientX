using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsMessageOptions.RecursionDesired"/> handling in DNS wire serialization.
    /// </summary>
    public class DnsMessageRecursionDesiredTests {
        /// <summary>
        /// Ensures the RD bit is cleared when recursion is not desired.
        /// </summary>
        [Fact]
        public void SerializeDnsWireFormat_ShouldClearRdBit_WhenRecursionDesiredFalse() {
            var opts = new DnsMessageOptions(RecursionDesired: false);
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);

            byte[] query = message.SerializeDnsWireFormat();

            Assert.Equal(0x00, query[2]);
            Assert.Equal(0x00, query[3]);
        }

        /// <summary>
        /// Ensures the RD bit is set when recursion is desired.
        /// </summary>
        [Fact]
        public void SerializeDnsWireFormat_ShouldSetRdBit_WhenRecursionDesiredTrue() {
            var opts = new DnsMessageOptions(RecursionDesired: true);
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);

            byte[] query = message.SerializeDnsWireFormat();

            Assert.Equal(0x01, query[2]);
            Assert.Equal(0x00, query[3]);
        }
    }
}
