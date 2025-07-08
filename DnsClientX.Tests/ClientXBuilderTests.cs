using System.Net;
using System.Reflection;
using System.Collections.Generic;
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
            var options = new EdnsOptions { EnableEdns = true, UdpBufferSize = 2048, Subnet = "192.0.2.0/24" };

            using var client = new ClientXBuilder()
                .WithEdnsOptions(options)
                .Build();

            Assert.Same(options, client.EndpointConfiguration.EdnsOptions);
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
    }
}

