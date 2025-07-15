using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests behavior when using a custom port with DNS over TLS.
    /// </summary>
    public class CustomDotPortTests {
        /// <summary>
        /// Ensures that selecting the hostname strategy does not alter a custom port.
        /// </summary>
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
