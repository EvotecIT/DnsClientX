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
