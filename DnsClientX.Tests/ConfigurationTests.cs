using System;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests for <see cref="Configuration"/>.
    /// </summary>
    public class ConfigurationTests {
        /// <summary>
        /// Constructing from a hostname should populate base URI and port.
        /// </summary>
        [Fact]
        public void ShouldCreateConfigurationFromHostname() {
            var config = new Configuration("1.1.1.1", DnsRequestFormat.DnsOverHttpsJSON);
            Assert.Equal(new Uri("https://1.1.1.1/dns-query"), config.BaseUri);
            Assert.Equal(443, config.Port);
        }

        /// <summary>
        /// Hostname is required when creating a custom configuration.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Constructor_ShouldThrowOnNullOrWhitespaceHostname(string? hostname) {
            Assert.Throws<ArgumentException>(() => new Configuration(hostname!, DnsRequestFormat.DnsOverHttpsJSON));
        }

        /// <summary>
        /// Changing the request format should update the default port.
        /// </summary>
        [Fact]
        public void ShouldUpdatePortWhenFormatChanges() {
            var config = new Configuration("1.1.1.1", DnsRequestFormat.DnsOverHttps);
            Assert.Equal(443, config.Port);

            config.RequestFormat = DnsRequestFormat.DnsOverTCP;
            Assert.Equal(53, config.Port);

            config.RequestFormat = DnsRequestFormat.DnsOverTLS;
            Assert.Equal(853, config.Port);

            config.RequestFormat = DnsRequestFormat.Multicast;
            Assert.Equal(5353, config.Port);
        }

        /// <summary>A relative trust-anchor path becomes absolute before a query snapshot.</summary>
        [Fact]
        public void TrustAnchorPath_ResolvesWhenConfigured() {
            string relativePath = Path.Combine("dnssec", "root-anchors.json");
            var config = new Configuration(DnsEndpoint.Cloudflare) {
                Rfc5011TrustAnchorStorePath = relativePath
            };

            string expectedPath = Path.GetFullPath(relativePath);
            Assert.Equal(expectedPath, config.Rfc5011TrustAnchorStorePath);
            Assert.Equal(expectedPath, config.CreateQuerySnapshot().Rfc5011TrustAnchorStorePath);
        }
    }
}
