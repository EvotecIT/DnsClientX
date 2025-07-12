using System.Net.Http;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class MaxConnectionsPerServerTests {
        [Fact]
        public void HandlerUsesConfigurationValue() {
            using ClientX client = new ClientX(
                DnsEndpoint.Cloudflare,
                maxConnectionsPerServer: 5);

            FieldInfo handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            HttpClientHandler handler = (HttpClientHandler)handlerField.GetValue(client)!;

            Assert.Equal(5, handler.MaxConnectionsPerServer);
        }
    }
}
