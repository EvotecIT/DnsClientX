using System;
using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Protects the resolver selection contract of default client creation.</summary>
    [Collection("NoParallel")]
    public class DefaultResolverTests {
        /// <summary>Default construction selects the OS resolver with UDP transport.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DefaultClient_UsesSystemResolver(bool useBuilder) {
            SystemInformation.SetDnsServerProvider(() => new List<string> { "192.0.2.53" });
            try {
                using ClientX client = useBuilder ? new ClientXBuilder().Build() : new ClientX();

                Assert.Equal(DnsEndpoint.System, client.EndpointConfiguration.BuiltInEndpoint);
                Assert.Equal(DnsRequestFormat.DnsOverUDP, client.EndpointConfiguration.RequestFormat);
                Assert.Equal("192.0.2.53", client.EndpointConfiguration.Hostname);
                Assert.Null(client.EndpointConfiguration.BaseUri);
                Assert.NotNull(client.EndpointConfiguration.SystemDnsConfiguration);
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }

        /// <summary>No configured OS resolver fails instead of selecting a public service.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DefaultClient_WithoutSystemResolver_DoesNotSelectPublicResolver(bool useBuilder) {
            SystemInformation.SetDnsServerProvider(() => new List<string>());
            try {
                Assert.Throws<InvalidOperationException>(() => {
                    using ClientX client = useBuilder ? new ClientXBuilder().Build() : new ClientX();
                });
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }

        /// <summary>Public fallback remains available only through explicit constructor opt-in.</summary>
        [Fact]
        public void DefaultClient_PublicFallbackRequiresOptIn() {
            SystemInformation.SetDnsServerProvider(() => new List<string>());
            try {
                using var client = new ClientX(systemDnsFallback: SystemDnsFallback.PublicResolvers);

                Assert.Equal(DnsEndpoint.System, client.EndpointConfiguration.BuiltInEndpoint);
                Assert.Equal(SystemDnsDiscoverySource.PublicFallback, client.EndpointConfiguration.SystemDnsConfiguration!.Source);
                Assert.Equal("1.1.1.1", client.EndpointConfiguration.Hostname);
                Assert.Equal(DnsRequestFormat.DnsOverUDP, client.EndpointConfiguration.RequestFormat);
            } finally {
                SystemInformation.SetDnsServerProvider(null);
            }
        }

        /// <summary>Explicit Cloudflare selection retains the existing JSON DoH profile.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExplicitCloudflare_PreservesJsonOverHttps(bool useBuilder) {
            using ClientX client = useBuilder
                ? new ClientXBuilder().WithEndpoint(DnsEndpoint.Cloudflare).Build()
                : new ClientX(DnsEndpoint.Cloudflare);

            Assert.Equal(DnsEndpoint.Cloudflare, client.EndpointConfiguration.BuiltInEndpoint);
            Assert.Equal(DnsRequestFormat.DnsOverHttpsJSON, client.EndpointConfiguration.RequestFormat);
            Assert.Equal("1.1.1.1", client.EndpointConfiguration.Hostname);
            Assert.Equal(new Uri("https://1.1.1.1/dns-query"), client.EndpointConfiguration.BaseUri);
        }
    }
}
