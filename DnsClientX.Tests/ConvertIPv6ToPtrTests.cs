using Xunit;

namespace DnsClientX.Tests {
    public class ConvertIPv6ToPtrTests {
        [Fact]
        public void ConvertsIpv6AndTrims() {
            var result = ClientX.ConvertIPv6ToPtr(" 2001:db8::1 ");
            Assert.Equal("1.0.0.0.0.0.0.0.0.0.0.0.0.0.0.0.8.b.d.0.1.0.0.2.ip6.arpa", result);
        }

        [Fact]
        public void ReturnsInputForInvalidOrIpv4() {
            Assert.Equal("1.2.3.4", ClientX.ConvertIPv6ToPtr("1.2.3.4"));
            Assert.Equal("notanipv6", ClientX.ConvertIPv6ToPtr("notanipv6"));
        }
    }
}
