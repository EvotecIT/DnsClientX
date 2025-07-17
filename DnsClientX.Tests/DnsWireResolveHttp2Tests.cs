#if NET5_0_OR_GREATER
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the HTTP/2 DNS wire resolver.
    /// </summary>
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

        private class Http2ErrorHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
                    Content = new StringContent("server error")
                };
                response.Version = HttpVersion.Version20;
                return Task.FromResult(response);
            }
        }

        private class Http2InvalidHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                throw new InvalidOperationException("invalid op");
            }
        }

        /// <summary>
        /// Ensures HTTP/2 is used when requesting DNS over HTTP/2.
        /// </summary>
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

        /// <summary>
        /// Validates that the response body is included in thrown exceptions.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatHttp2_IncludesBodyOnError() {
            var handler = new Http2ErrorHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp2);

            var ex = await Assert.ThrowsAsync<DnsClientException>(() =>
                DnsWireResolveHttp2.ResolveWireFormatHttp2(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None));

            Assert.Contains("server error", ex.Message);
            Assert.Equal(config.Hostname, ex.Response.Questions[0].HostName);
            Assert.Equal(config.Port, ex.Response.Questions[0].Port);
        }

        /// <summary>
        /// Ensures an invalid operation results in a server failure response.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatHttp2_ReturnsServerFailureOnInvalidOperation() {
            var handler = new Http2InvalidHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp2);

            var response = await DnsWireResolveHttp2.ResolveWireFormatHttp2(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            Assert.Contains("invalid op", response.Error);
        }
    }
}
#endif
