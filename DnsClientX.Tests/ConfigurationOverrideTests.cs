using System;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class ConfigurationOverrideTests {
        [Fact]
        public void ShouldOverrideUserAgent() {
            const string customUa = "MyApp/1.0";
            using var client = new ClientX(DnsEndpoint.Cloudflare, userAgent: customUa);
            Assert.Equal(customUa, client.EndpointConfiguration.UserAgent);
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var httpClient = (HttpClient)clientField.GetValue(client)!;
            Assert.Contains(customUa, httpClient.DefaultRequestHeaders.UserAgent.ToString());
        }

        [Fact]
        public void ShouldOverrideHttpVersion() {
            var version = new Version(1, 1);
            using var client = new ClientX(DnsEndpoint.Cloudflare, httpVersion: version);
            Assert.Equal(version, client.EndpointConfiguration.HttpVersion);
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var httpClient = (HttpClient)clientField.GetValue(client)!;
#if NETCOREAPP2_1_OR_GREATER || NET5_0_OR_GREATER
            Assert.Equal(version, httpClient.DefaultRequestVersion);
#endif
        }

        [Fact]
        public void ShouldOverrideTcpFallback() {
            using var client = new ClientX(DnsEndpoint.Cloudflare, useTcpFallback: false);
            Assert.False(client.EndpointConfiguration.UseTcpFallback);
        }

        [Fact]
        public void CustomEndpoint_ShouldAllowOverrides() {
            using var client = new ClientX(DnsEndpoint.Custom);
            client.EndpointConfiguration.Hostname = "1.1.1.1";
            client.EndpointConfiguration.RequestFormat = DnsRequestFormat.DnsOverHttpsJSON;
            client.EndpointConfiguration.BaseUri = new Uri($"https://{client.EndpointConfiguration.Hostname}/dns-query");

            Assert.Equal("1.1.1.1", client.EndpointConfiguration.Hostname);
            Assert.Equal(new Uri("https://1.1.1.1/dns-query"), client.EndpointConfiguration.BaseUri);
        }
    }
}
