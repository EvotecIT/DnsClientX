#if NET5_0_OR_GREATER
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the HTTP/3 DNS wire resolver.
    /// </summary>
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
        private class Http3ErrorHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError) {
                    Content = new StringContent("server error")
                };
                response.Version = HttpVersion.Version30;
                return Task.FromResult(response);
            }
        }
        private class Http3EmptyHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
                response.Version = HttpVersion.Version30;
                return Task.FromResult(response);
            }
        }

        private class Http3InvalidHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                throw new InvalidOperationException("invalid op");
            }
        }


        /// <summary>
        /// Ensures HTTP/3 is used when available.
        /// </summary>
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
        /// <summary>
        /// Validates that error responses include the body when using HTTP/3.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatHttp3_IncludesBodyOnError() {
            var handler = new Http3ErrorHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp3);

            var ex = await Assert.ThrowsAsync<DnsClientException>(() =>
                DnsWireResolveHttp3.ResolveWireFormatHttp3(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None));

            Assert.Contains("server error", ex.Message);
            Assert.Equal(config.Hostname, ex.Response.Questions[0].HostName);
            Assert.Equal(config.Port, ex.Response.Questions[0].Port);
        }

        /// <summary>
        /// Ensures an invalid operation results in a server failure response.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatHttp3_ReturnsServerFailureOnInvalidOperation() {
            var handler = new Http3InvalidHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp3);

            var response = await DnsWireResolveHttp3.ResolveWireFormatHttp3(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None);

            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            Assert.Contains("invalid op", response.Error);
        }
        
        /// <summary>
        /// Validates that an empty HTTP/3 response triggers an exception.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatHttp3_ThrowsOnEmptyResponse() {
            var handler = new Http3EmptyHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp3);

            var ex = await Assert.ThrowsAsync<DnsClientException>(() =>
                DnsWireResolveHttp3.ResolveWireFormatHttp3(client, "example.com", DnsRecordType.A, false, false, false, config, CancellationToken.None));

            Assert.Contains("empty response", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(HttpStatusCode.OK.ToString(), ex.Message);
        }

    }
}
#endif
