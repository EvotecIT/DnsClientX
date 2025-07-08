#if NET5_0_OR_GREATER
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsWireResolveHttp2Tests {
        private class Http2Handler : HttpMessageHandler {
            public HttpRequestMessage? Request { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Request = request;
                byte[] responseBytes = { 0x00, 0x01, 0x81, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(responseBytes) };
                response.Version = HttpVersion.Version20;
                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task ResolveWireFormatHttp2_UsesHttp2() {
            var handler = new Http2Handler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp2);
            var response = await DnsWireResolveHttp2.ResolveWireFormatHttp2(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

            Assert.Equal(HttpVersion.Version20, handler.Request?.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionOrHigher, handler.Request?.VersionPolicy);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
        }
    }
}
#endif
