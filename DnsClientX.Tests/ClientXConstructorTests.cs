using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for ClientX constructors.
    /// </summary>
    public class ClientXConstructorTests {
        /// <summary>
        /// Ensures the configuration constructor uses the provided configuration instance.
        /// </summary>
        [Fact]
        public void ConfigurationConstructor_UsesProvidedConfiguration() {
            var config = new Configuration(new Uri("https://1.1.1.1/dns-query"), DnsRequestFormat.DnsOverHttpsJSON);

            using var client = new ClientX(config);

            Assert.Same(config, client.EndpointConfiguration);
            Assert.Equal(DnsRequestFormat.DnsOverHttpsJSON, client.EndpointConfiguration.RequestFormat);
            Assert.Equal("1.1.1.1", client.EndpointConfiguration.Hostname);
            Assert.Equal(new Uri("https://1.1.1.1/dns-query"), client.EndpointConfiguration.BaseUri);
        }
    }
}
