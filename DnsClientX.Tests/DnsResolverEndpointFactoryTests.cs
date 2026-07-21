using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for converting built-in providers to multi-resolver endpoints and endpoint formatting.
    /// </summary>
    public class DnsResolverEndpointFactoryTests {
        /// <summary>
        /// Cloudflare providers map to DoH endpoints on port 443.
        /// </summary>
        [Fact]
        public void From_Cloudflare_ReturnsDoHEndpoints() {
            var eps = DnsResolverEndpointFactory.From(DnsEndpoint.Cloudflare);
            Assert.NotEmpty(eps);
            Assert.All(eps, e => Assert.Equal(Transport.Doh, e.Transport));
            Assert.All(eps, e => Assert.True(e.Port == 443));
        }

        /// <summary>
        /// Root servers map to UDP endpoints on port 53.
        /// </summary>
        [Fact]
        public void From_RootServer_ReturnsUdpEndpoints() {
            var eps = DnsResolverEndpointFactory.From(DnsEndpoint.RootServer);
            Assert.NotEmpty(eps);
            Assert.All(eps, e => Assert.Equal(Transport.Udp, e.Transport));
            Assert.All(eps, e => Assert.Equal(53, e.Port));
        }

        /// <summary>
        /// Provider variants should preserve their distinct hosts when expanded.
        /// </summary>
        [Fact]
        public void From_VariantProviders_PreservesDistinctHosts() {
            var quad9 = DnsResolverEndpointFactory.From(DnsEndpoint.Quad9ECS);
            Assert.Single(quad9);
            Assert.Equal("dns11.quad9.net", quad9[0].Host);

            var openDnsFamily = DnsResolverEndpointFactory.From(DnsEndpoint.OpenDNSFamily);
            Assert.Contains(openDnsFamily, e => e.Host == "208.67.222.123");
            Assert.Contains(openDnsFamily, e => e.Host == "208.67.220.123");
        }

        /// <summary>
        /// Request format overrides should be preserved for non-default transports.
        /// </summary>
        [Fact]
        public void From_SupportedAdvancedProviders_PreservesRequestFormat() {
            var cloudflarePost = DnsResolverEndpointFactory.From(DnsEndpoint.CloudflareWireFormatPost);
            Assert.All(cloudflarePost, e => Assert.Equal(DnsRequestFormat.DnsOverHttpsWirePost, e.RequestFormat));

            var quad9Http3 = DnsResolverEndpointFactory.From(DnsEndpoint.Quad9Http3);
            Assert.Single(quad9Http3);
            Assert.Equal(DnsRequestFormat.DnsOverHttp3, quad9Http3[0].RequestFormat);
            Assert.Equal(Transport.Doh, quad9Http3[0].Transport);

            var quad9Quic = DnsResolverEndpointFactory.From(DnsEndpoint.Quad9Quic);
            Assert.Single(quad9Quic);
            Assert.Equal(DnsRequestFormat.DnsOverQuic, quad9Quic[0].RequestFormat);
            Assert.Equal(Transport.Quic, quad9Quic[0].Transport);
        }

        /// <summary>
        /// Provider names that do not correspond to published protocol endpoints fail explicitly.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.DnsCryptCloudflare)]
        public void From_UnsupportedProviderClaims_Throws(DnsEndpoint endpoint) {
            Assert.Throws<NotSupportedException>(() => DnsResolverEndpointFactory.From(endpoint));
        }

        /// <summary>
        /// Endpoint ToString should format DoH and host:port consistently.
        /// </summary>
        [Fact]
        public void DnsResolverEndpoint_ToString_Formats() {
            var doh = new DnsResolverEndpoint { Transport = Transport.Doh, DohUrl = new Uri("https://dns.google/dns-query"), Port = 443, Host = "dns.google" };
            Assert.Equal("https://dns.google/dns-query", doh.ToString());

            var udp = new DnsResolverEndpoint { Transport = Transport.Udp, Host = "1.1.1.1", Port = 53 };
            Assert.Equal("1.1.1.1:53", udp.ToString());
        }
    }
}
