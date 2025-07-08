#if NET8_0_OR_GREATER
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsWireResolveHttp3Tests {
        private class Http3Handler : HttpMessageHandler {
            public HttpRequestMessage? Request { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Request = request;
                byte[] responseBytes = { 0x00, 0x01, 0x81, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(responseBytes) };
                response.Version = HttpVersion.Version30;
                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task ResolveWireFormatHttp3_UsesHttp3() {
            var handler = new Http3Handler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp3);
            var response = await DnsWireResolveHttp3.ResolveWireFormatHttp3(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

            Assert.Equal(HttpVersion.Version30, handler.Request?.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionOrHigher, handler.Request?.VersionPolicy);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
        }
    }
}
#endif
