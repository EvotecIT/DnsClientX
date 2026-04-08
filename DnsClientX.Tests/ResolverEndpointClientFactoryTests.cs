using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests custom resolver endpoint client creation helpers.
    /// </summary>
    public class ResolverEndpointClientFactoryTests {
        /// <summary>
        /// Ensures explicit request formats are preserved when creating DoH clients from resolver endpoints.
        /// </summary>
        [Fact]
        public void CreateClient_UsesExplicitRequestFormatForDohEndpoint() {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Transport = Transport.Doh,
                Host = "dns.google",
                Port = 443,
                DohUrl = new Uri("https://dns.google/resolve"),
                RequestFormat = DnsRequestFormat.DnsOverHttpsJSON
            });

            Assert.Equal(DnsRequestFormat.DnsOverHttpsJSON, client.EndpointConfiguration.RequestFormat);
            Assert.Equal(new Uri("https://dns.google/resolve"), client.EndpointConfiguration.BaseUri);
        }

        /// <summary>
        /// Ensures explicit non-default DoH request formats are preserved for URL-based endpoint creation.
        /// </summary>
        [Fact]
        public void CreateClient_UsesExplicitHttp3RequestFormatForDohEndpoint() {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Transport = Transport.Doh,
                Host = "cloudflare-dns.com",
                Port = 443,
                RequestFormat = DnsRequestFormat.DnsOverHttp3
            });

            Assert.Equal(DnsRequestFormat.DnsOverHttp3, client.EndpointConfiguration.RequestFormat);
            Assert.Equal(new Uri("https://cloudflare-dns.com/dns-query"), client.EndpointConfiguration.BaseUri);
        }

        /// <summary>
        /// Ensures configured resolver descriptions use the effective client host and port.
        /// </summary>
        [Fact]
        public void DescribeConfiguredResolver_ReturnsHostAndPort() {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Transport = Transport.Tcp,
                Host = "1.1.1.1",
                Port = 53
            });

            Assert.Equal("1.1.1.1:53", ResolverEndpointClientFactory.DescribeConfiguredResolver(client));
        }

        /// <summary>
        /// Ensures configured resolver descriptions use the effective DoH host and port.
        /// </summary>
        [Fact]
        public void DescribeConfiguredResolver_ReturnsDohHostAndPort() {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Transport = Transport.Doh,
                DohUrl = new Uri("https://dns.example:4443/dns-query"),
                RequestFormat = DnsRequestFormat.DnsOverHttpsPOST
            });

            Assert.Equal("dns.example:4443", ResolverEndpointClientFactory.DescribeConfiguredResolver(client));
        }
    }
}
