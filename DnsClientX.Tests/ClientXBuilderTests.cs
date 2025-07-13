using System;
using System.Net;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
using Xunit;

namespace DnsClientX.Tests {
    public class ClientXBuilderTests {
        [Fact]
        public void BuildShouldApplySettings() {
            var proxy = new WebProxy("http://localhost:8080");

            using var client = new ClientXBuilder()
                .WithEndpoint(DnsEndpoint.GoogleWireFormatPost)
                .WithTimeout(2000)
                .WithProxy(proxy)
                .Build();

            Assert.Equal(2000, client.EndpointConfiguration.TimeOut);
            Assert.NotNull(client.EndpointConfiguration.BaseUri);
            Assert.StartsWith("https://8.8.8.8", client.EndpointConfiguration.BaseUri!.ToString());

            var field = typeof(ClientX).GetField("_webProxy", BindingFlags.NonPublic | BindingFlags.Instance)!;
            Assert.Same(proxy, field.GetValue(client));
        }

        [Fact]
        public void BuildShouldApplyEdnsOptions() {
            var options = new EdnsOptions { EnableEdns = true, UdpBufferSize = 2048, Subnet = new EdnsClientSubnetOption("192.0.2.0/24") };

            using var client = new ClientXBuilder()
                .WithEdnsOptions(options)
                .Build();

            Assert.Same(options, client.EndpointConfiguration.EdnsOptions);
        }

        [Fact]
        public void BuildShouldApplySigningKey() {
            using var rsa = RSA.Create();
            using var client = new ClientXBuilder()
                .WithSigningKey(rsa)
                .Build();

            Assert.Same(rsa, client.EndpointConfiguration.SigningKey);
        }

        [Fact]
        public void BuildShouldValidateHostnames() {
            using var client = new ClientXBuilder()
                .WithEndpoint(DnsEndpoint.Cloudflare)
                .Build();

            Assert.NotNull(client.EndpointConfiguration.Hostname);
        }

        [Fact]
        public void BuildShouldThrowOnInvalidHostname() {
            var field = typeof(SystemInformation).GetField("cachedDnsServers", BindingFlags.NonPublic | BindingFlags.Static)!;
            var original = (Lazy<List<string>>)field.GetValue(null)!;
            try {
                field.SetValue(null, new Lazy<List<string>>(() => new List<string> { "inv@lid_host" }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
                Assert.Throws<ArgumentException>(() => new ClientXBuilder().WithEndpoint(DnsEndpoint.System).Build());
            } finally {
                field.SetValue(null, original);
            }
        }

        [Fact]
        public void WithEndpointCustom_ShouldThrowImmediately() {
            Assert.Throws<ArgumentException>(() => new ClientXBuilder().WithEndpoint(DnsEndpoint.Custom));
        }

        [Fact]
        public void BuildShouldApplyAdvancedSettings() {
            var version = new Version(2, 0);

            using var client = new ClientXBuilder()
                .WithEndpoint(DnsEndpoint.Google)
                .WithSelectionStrategy(DnsSelectionStrategy.Random)
                .WithUserAgent("UnitTest")
                .WithHttpVersion(version)
                .WithIgnoreCertificateErrors()
                .WithEnableCache()
                .WithUseTcpFallback(false)
                .Build();

            Assert.Equal(DnsSelectionStrategy.Random, client.EndpointConfiguration.SelectionStrategy);
            Assert.Equal("UnitTest", client.EndpointConfiguration.UserAgent);
            Assert.Equal(version, client.EndpointConfiguration.HttpVersion);
            Assert.True(client.IgnoreCertificateErrors);
            Assert.True(client.CacheEnabled);
            Assert.False(client.EndpointConfiguration.UseTcpFallback);
        }

        [Fact]
        public void BuildWithHostname_ShouldConfigureCustomEndpoint() {
            using var client = new ClientXBuilder()
                .WithHostname("1.1.1.1", DnsRequestFormat.DnsOverHttps)
                .Build();

            Assert.Equal("1.1.1.1", client.EndpointConfiguration.Hostname);
            Assert.Equal(DnsRequestFormat.DnsOverHttps, client.EndpointConfiguration.RequestFormat);
        }
    }
}

