using System.Net;
using System.Net.Http;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class ProxyTests {
        [Fact]
        public void ShouldConfigureProxyOnHandler() {
            var proxy = new WebProxy("http://localhost:1234");
            using var client = new ClientX(DnsEndpoint.Cloudflare, proxy: proxy);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var handler = (HttpClientHandler)handlerField.GetValue(client)!;
            Assert.Equal(proxy, handler.Proxy);
        }
    }
}
