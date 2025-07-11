using System.Net.Http;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class MaxConnectionsPerServerTests {
        [Fact]
        public void HandlerUsesConfigurationValue() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var handler = (HttpClientHandler)handlerField.GetValue(client)!;
            Assert.Equal(client.EndpointConfiguration.MaxConnectionsPerServer, handler.MaxConnectionsPerServer);
        }
    }
}
