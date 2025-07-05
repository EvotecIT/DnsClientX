using System.Net;
using System.Reflection;
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
    }
}

