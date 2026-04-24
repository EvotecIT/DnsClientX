using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsMessageOptions"/> serialization.
    /// </summary>
    public class DnsMessageOptionsTests {
        private static void AssertEcsOption(byte[] query, string name) {
            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;
            Assert.Equal(0, query[offset]);
            offset += 1;
            ushort type = (ushort)((query[offset] << 8) | query[offset + 1]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
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
