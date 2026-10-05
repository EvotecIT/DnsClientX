using System;
using System.Net.Sockets;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests custom resolver endpoint client creation helpers.
    /// </summary>
    public class ResolverEndpointClientFactoryTests {
        /// <summary>Omitted ports follow the effective protocol; explicitly assigned ports remain authoritative.</summary>
        [Theory]
        [InlineData(Transport.Udp, 53)]
        [InlineData(Transport.Dot, 853)]
        [InlineData(Transport.Quic, 853)]
        [InlineData(Transport.Doh, 443)]
        [InlineData(Transport.Grpc, 443)]
        public void EndpointDefaultPortMatchesTransport(Transport transport, int expected) {
            Assert.Equal(expected, new DnsResolverEndpoint { Transport = transport }.Port);
            Assert.Equal(53, new DnsResolverEndpoint { Transport = transport, Port = 53 }.Port);
        }

        /// <summary>An explicit request format also determines the default port and endpoint URI.</summary>
        [Fact]
        public void CreateClient_RequestFormatOverrideUsesProtocolDefaultPort() {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Host = "dns.example", RequestFormat = DnsRequestFormat.DnsOverHttps
            });
            Assert.Equal(new Uri("https://dns.example/dns-query"), client.EndpointConfiguration.BaseUri);
            Assert.Equal(443, client.EndpointConfiguration.Port);
        }
        /// <summary>Explicit endpoint security, deadline and wire options reach each transport.</summary>
        [Theory]
        [InlineData(Transport.Dot, DnsRequestFormat.DnsOverTLS)]
        [InlineData(Transport.Doh, DnsRequestFormat.DnsOverHttpsJSON)]
        public void CreateClient_PreservesEndpointBehavior(Transport transport, DnsRequestFormat format) {
            using var client = ResolverEndpointClientFactory.CreateClient(new DnsResolverEndpoint {
                Transport = transport, RequestFormat = format, Host = "127.0.0.1", Port = 8443,
                TlsServerName = "resolver.example", Family = AddressFamily.InterNetwork,
                Timeout = TimeSpan.FromMilliseconds(321), AllowTcpFallback = false, EdnsBufferSize = 1232
            });
            var actual = client.EndpointConfiguration;
            Assert.Equal("resolver.example", actual.TlsServerName);
            Assert.Equal(AddressFamily.InterNetwork, actual.PreferredAddressFamily);
            Assert.Equal(321, actual.TimeOut);
            Assert.False(actual.UseTcpFallback);
            Assert.Equal(1232, actual.UdpBufferSize);
            Assert.Equal(8443, actual.Port);
            Assert.False(client.IgnoreCertificateErrors);
        }
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

        /// <summary>Unsupported protocol identifiers fail when the client is created, not after network work begins.</summary>
        [Theory]
        [InlineData(DnsRequestFormat.ObliviousDnsOverHttps)]
        [InlineData(DnsRequestFormat.DnsCrypt)]
        [InlineData(DnsRequestFormat.DnsCryptRelay)]
        public void CreateClient_RejectsReservedUnsupportedProtocols(DnsRequestFormat requestFormat) {
            var endpoint = new DnsResolverEndpoint {
                Transport = requestFormat == DnsRequestFormat.ObliviousDnsOverHttps ? Transport.Doh : Transport.Udp,
                Host = "resolver.example",
                Port = requestFormat == DnsRequestFormat.ObliviousDnsOverHttps ? 443 : 53,
                DohUrl = requestFormat == DnsRequestFormat.ObliviousDnsOverHttps
                    ? new Uri("https://resolver.example/dns-query")
                    : null,
                RequestFormat = requestFormat
            };

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
                ResolverEndpointClientFactory.CreateClient(endpoint));

            Assert.Equal(DnsTransportCapabilities.GetUnsupportedMessage(requestFormat), exception.Message);
        }
    }
}
