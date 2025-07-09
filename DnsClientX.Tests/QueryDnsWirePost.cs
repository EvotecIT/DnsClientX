using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsWirePost {
        private class WirePostHandler : HttpMessageHandler {
            public HttpRequestMessage? Request { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Request = request;
                byte[] responseBytes = { 0x00, 0x01, 0x81, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(responseBytes) };
                return Task.FromResult(response);
            }
        }

        private static void InjectClient(ClientX client, HttpClient httpClient) {
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(client)!;
            clients[client.EndpointConfiguration.SelectionStrategy] = httpClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(client, httpClient);
        }

        [Theory]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        public async Task ShouldPostWire(DnsEndpoint endpoint) {
            var handler = new WirePostHandler();
            using var clientX = new ClientX(endpoint);
            var httpClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            InjectClient(clientX, httpClient);

            var response = await clientX.Resolve("evotec.pl", DnsRecordType.A, retryOnTransient: false);

            Assert.Equal(HttpMethod.Post, handler.Request?.Method);
            Assert.Equal("application/dns-message", handler.Request?.Content?.Headers.ContentType?.MediaType);
            Assert.NotNull(response);
        }
    }
}
