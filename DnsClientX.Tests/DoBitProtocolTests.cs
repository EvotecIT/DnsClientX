using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DoBitProtocolTests {
        private static void AssertDoBit(byte[] query, string name) {
            int additionalCount = (query[10] << 8) | query[11];
            Assert.Equal(1, additionalCount);

            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;

            Assert.Equal(0, query[offset]);
            ushort type = (ushort)((query[offset + 1] << 8) | query[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            uint ttl = (uint)((query[offset + 5] << 24) | (query[offset + 6] << 16) | (query[offset + 7] << 8) | query[offset + 8]);
            Assert.Equal(0x00008000u, ttl);
        }

        [Fact]
        public void DotRequest_ShouldIncludeDoBit_WhenRequested() {
            var message = new DnsMessage("example.com", DnsRecordType.A, true);
            byte[] data = message.SerializeDnsWireFormat();
            AssertDoBit(data, "example.com");
        }

        private static byte[] DecodeBase64Url(string input) {
            string base64 = input.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4) {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }

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
        public async Task DohRequest_ShouldIncludeDoBit_WhenRequested() {
            var handler = new Http2Handler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp2);
            await DnsWireResolveHttp2.ResolveWireFormatHttp2(client, "example.com", DnsRecordType.A, true, false, false, config, CancellationToken.None);
            string query = handler.Request!.RequestUri!.Query.Replace("?dns=", string.Empty);
            byte[] bytes = DecodeBase64Url(query);
            AssertDoBit(bytes, "example.com");
        }

#if NET5_0_OR_GREATER
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
        public async Task Doh3Request_ShouldIncludeDoBit_WhenRequested() {
            var handler = new Http3Handler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/dns-query") };
            var config = new Configuration(new Uri("https://example.com/dns-query"), DnsRequestFormat.DnsOverHttp3);
            await DnsWireResolveHttp3.ResolveWireFormatHttp3(client, "example.com", DnsRecordType.A, true, false, false, config, CancellationToken.None);
            string query = handler.Request!.RequestUri!.Query.Replace("?dns=", string.Empty);
            byte[] bytes = DecodeBase64Url(query);
            AssertDoBit(bytes, "example.com");
        }
#endif

        [Fact]
        public void DoqRequest_ShouldIncludeDoBit_WhenRequested() {
            var message = new DnsMessage("example.com", DnsRecordType.A, true);
            byte[] data = message.SerializeDnsWireFormat();
            AssertDoBit(data, "example.com");
        }
    }
}
