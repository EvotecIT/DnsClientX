using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for composing the NSID EDNS option.
    /// </summary>
    public class NsidOptionTests {
        [Fact]
        public void SerializeDnsWireFormat_ShouldIncludeNsidOption() {
            var option = new NsidOption();
            var message = new DnsMessage("example.com", DnsRecordType.A, false, true, 4096, null, false, null, new[] { option });
            byte[] data = message.SerializeDnsWireFormat();

            int offset = 12;
            foreach (var label in "example.com".Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;

            Assert.Equal(1, (data[10] << 8) | data[11]);
            Assert.Equal(0, data[offset]);
            ushort type = (ushort)((data[offset + 1] << 8) | data[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            offset += 1 + 2 + 2 + 4;
            ushort rdlen = (ushort)((data[offset] << 8) | data[offset + 1]);
            Assert.True(rdlen > 0);
            offset += 2;
            ushort optionCode = (ushort)((data[offset] << 8) | data[offset + 1]);
            Assert.Equal(3, optionCode);
            ushort optionLen = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            Assert.Equal<ushort>(0, optionLen);
        }
    }
}
