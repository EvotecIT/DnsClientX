using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for converting built-in providers to multi-resolver endpoints and endpoint formatting.
    /// </summary>
    public class DnsResolverEndpointFactoryTests {
        /// <summary>Single-provider and multi-resolver queries select the published DoH filtering profiles.</summary>
        [Theory]
        [InlineData(DnsEndpoint.AdGuard, "https://dns.adguard-dns.com/dns-query")]
        [InlineData(DnsEndpoint.AdGuardFamily, "https://family.adguard-dns.com/dns-query")]
        [InlineData(DnsEndpoint.AdGuardNonFiltering, "https://unfiltered.adguard-dns.com/dns-query")]
        [InlineData(DnsEndpoint.CleanBrowsingSecurity, "https://doh.cleanbrowsing.org/doh/security-filter/")]
        [InlineData(DnsEndpoint.CleanBrowsingAdult, "https://doh.cleanbrowsing.org/doh/adult-filter/")]
        [InlineData(DnsEndpoint.CleanBrowsingFamily, "https://doh.cleanbrowsing.org/doh/family-filter/")]
        [InlineData(DnsEndpoint.ControlD, "https://freedns.controld.com/p0")]
        [InlineData(DnsEndpoint.ControlDMalware, "https://freedns.controld.com/p1")]
        [InlineData(DnsEndpoint.ControlDAds, "https://freedns.controld.com/p2")]
        [InlineData(DnsEndpoint.ControlDFamily, "https://freedns.controld.com/family")]
        public void From_PublicDohProfile_UsesPublishedUrl(DnsEndpoint provider, string expectedUrl) {
            var configuration = new Configuration(provider);
            var endpoint = Assert.Single(DnsResolverEndpointFactory.From(provider));
            using var client = ResolverEndpointClientFactory.CreateClient(endpoint);

            Assert.Equal(new Uri(expectedUrl), configuration.BaseUri);
            Assert.Equal(new Uri(expectedUrl), client.EndpointConfiguration.BaseUri);
            Assert.Equal(DnsRequestFormat.DnsOverHttps, configuration.RequestFormat);
            Assert.Equal(DnsRequestFormat.DnsOverHttps, client.EndpointConfiguration.RequestFormat);
            Assert.Equal(Transport.Doh, endpoint.Transport);
            Assert.Equal(443, endpoint.Port);
        }

        /// <summary>Plain-DNS profiles retain their published redundant IPv4/IPv6 addresses without claiming encryption.</summary>
        [Theory]
        [InlineData(DnsEndpoint.ComodoSecureDns, new[] { "8.26.56.26", "8.20.247.20" })]
        [InlineData(DnsEndpoint.DnsWatch, new[] {
            "84.200.69.80", "84.200.70.40", "2001:1608:10:25::1c04:b12f", "2001:1608:10:25::9249:d69b"
        })]
        public void From_PublicPlainDnsProfile_PreservesAddressesAndTransport(DnsEndpoint provider, string[] expectedHosts) {
            var configuration = new Configuration(provider);
            var endpoints = DnsResolverEndpointFactory.From(provider);

            Assert.Equal(expectedHosts, configuration.Hostnames);
            Assert.Equal(expectedHosts, endpoints.Select(endpoint => endpoint.Host));
            Assert.Equal(expectedHosts[0], configuration.Hostname);
            Assert.Equal(DnsRequestFormat.DnsOverUDP, configuration.RequestFormat);
            Assert.Equal(53, configuration.Port);
            Assert.Null(configuration.BaseUri);
            Assert.All(endpoints, endpoint => {
                using var client = ResolverEndpointClientFactory.CreateClient(endpoint);
                Assert.Equal(Transport.Udp, endpoint.Transport);
                Assert.Equal(DnsRequestFormat.DnsOverUDP, client.EndpointConfiguration.RequestFormat);
                Assert.Equal(endpoint.Host, client.EndpointConfiguration.Hostname);
                Assert.Equal(53, client.EndpointConfiguration.Port);
                Assert.Null(client.EndpointConfiguration.BaseUri);
            });
        }

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
