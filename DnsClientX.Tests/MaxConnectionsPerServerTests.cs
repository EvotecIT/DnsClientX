using System.Net.Http;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests that the HTTP handler honors the configured <c>MaxConnectionsPerServer</c> value.
    /// </summary>
    public class MaxConnectionsPerServerTests {
        /// <summary>
        /// Verifies that the handler uses the configuration value for maximum connections.
        /// </summary>
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
