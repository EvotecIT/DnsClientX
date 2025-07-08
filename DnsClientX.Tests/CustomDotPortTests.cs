using Xunit;

namespace DnsClientX.Tests {
    public class CustomDotPortTests {
        [Fact]
        public void SelectHostNameStrategy_ShouldKeepCustomPort() {
            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTLS) { Port = 8443 };
            config.SelectHostNameStrategy();
            Assert.Equal(8443, config.Port);
            Assert.NotNull(config.BaseUri);
            Assert.Equal(8443, config.BaseUri!.Port);
        }
    }
}
