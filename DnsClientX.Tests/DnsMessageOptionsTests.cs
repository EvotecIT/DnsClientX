using Xunit;

namespace DnsClientX.Tests {
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

        [Fact]
        public void SerializeDnsWireFormat_ShouldIncludeEcsOption_WhenUsingOptionsStruct() {
            var opts = new DnsMessageOptions(EnableEdns: true, Subnet: new EdnsClientSubnetOption("192.0.2.1/24"));
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);
            byte[] data = message.SerializeDnsWireFormat();
            AssertEcsOption(data, "example.com");
        }
    }
}
