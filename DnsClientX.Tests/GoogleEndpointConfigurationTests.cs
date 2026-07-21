using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests the published Google JSON endpoint configuration.</summary>
    public class GoogleEndpointConfigurationTests {
        /// <summary>
        /// The documented hostname avoids the inconsistent legacy JSON behavior observed on individual anycast IPs.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.Google, DnsRequestFormat.DnsOverHttps)]
        [InlineData(DnsEndpoint.GoogleWireFormat, DnsRequestFormat.DnsOverHttps)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost, DnsRequestFormat.DnsOverHttpsWirePost)]
        public void JsonEndpoint_UsesPublishedHostname(DnsEndpoint endpoint, DnsRequestFormat format) {
            var configuration = new Configuration(endpoint);

            Assert.Equal("dns.google", configuration.Hostname);
            Assert.Equal("https://dns.google/dns-query", configuration.BaseUri!.ToString().TrimEnd('/'));
            Assert.Equal(format, configuration.RequestFormat);
        }

        /// <summary>Built-in profiles reject protocols their providers do not publish.</summary>
        [Theory]
        [InlineData(DnsEndpoint.GoogleJsonPost)]
        [InlineData(DnsEndpoint.CloudflareJsonPost)]
        public void JsonPostEndpoint_FailsExplicitly(DnsEndpoint endpoint) {
            Assert.Throws<NotSupportedException>(() => new Configuration(endpoint));
        }
    }
}
