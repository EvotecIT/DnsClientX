using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsMessageOptions"/> serialization.
    /// </summary>
    public class DnsMessageOptionsTests {
        private static void AssertEcsOption(byte[] query, string name) {
            var reader = new DnsWireReader(query, 12);
            Assert.Equal(name + ".", reader.ReadName());
            reader.Skip(4);
            Assert.Equal(".", reader.ReadName());
            Assert.Equal((ushort)DnsRecordType.OPT, reader.ReadUInt16());
            reader.Skip(6);
            Assert.Equal(11, reader.ReadUInt16());
            Assert.Equal(8, reader.ReadUInt16());
            Assert.Equal(7, reader.ReadUInt16());
            Assert.Equal(new byte[] { 0, 1, 24, 0, 192, 0, 2 }, reader.ReadBytes(7));
            Assert.True(reader.IsAtEnd);
        }

        /// <summary>
        /// Ensures EDNS client subnet options are included in wire format serialization.
        /// </summary>
        [Fact]
        public void SerializeDnsWireFormat_ShouldIncludeEcsOption_WhenUsingOptionsStruct() {
            var opts = new DnsMessageOptions(EnableEdns: true, Subnet: new EdnsClientSubnetOption("192.0.2.1/24"));
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);
            byte[] data = message.SerializeDnsWireFormat();
            AssertEcsOption(data, "example.com");
        }

        /// <summary>
        /// Ensures fully-qualified names are encoded with one terminating root label.
        /// </summary>
        [Fact]
        public void SerializeDnsWireFormat_ShouldNotWriteExtraRootLabel_WhenNameHasTrailingDot() {
            var message = new DnsMessage("example.com.", DnsRecordType.A, requestDnsSec: false);
            byte[] data = message.SerializeDnsWireFormat();

            Assert.Equal(0, data[24]);
            ushort type = (ushort)((data[25] << 8) | data[26]);
            ushort @class = (ushort)((data[27] << 8) | data[28]);
            Assert.Equal((ushort)DnsRecordType.A, type);
            Assert.Equal(1, @class);
        }

        /// <summary>
        /// Ensures DoH GET serialization uses the same fully-qualified-name encoding.
        /// </summary>
        [Fact]
        public void ToBase64Url_ShouldNotWriteExtraRootLabel_WhenNameHasTrailingDot() {
            var message = new DnsMessage("example.com.", DnsRecordType.A, requestDnsSec: false);
            byte[] data = DecodeBase64Url(message.ToBase64Url());

            Assert.Equal(0, data[24]);
            ushort type = (ushort)((data[25] << 8) | data[26]);
            ushort @class = (ushort)((data[27] << 8) | data[28]);
            Assert.Equal((ushort)DnsRecordType.A, type);
            Assert.Equal(1, @class);
        }

        private static byte[] DecodeBase64Url(string value) {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            return System.Convert.FromBase64String(base64);
        }
    }
}
