using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsJsonPost {
        private class JsonPostHandler : HttpMessageHandler {
            public HttpRequestMessage? Request { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Request = request;
                if (request.Content != null) {
                    // no-op: ensure content is created but do not read
                }
                var json = "{\"Status\":0,\"Answer\":[{\"name\":\"evotec.pl\",\"type\":1,\"TTL\":60,\"data\":\"1.1.1.1\"}]}";
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
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
        [InlineData(DnsEndpoint.CloudflareJsonPost)]
        [InlineData(DnsEndpoint.GoogleJsonPost)]
        public async Task ShouldPostJson(DnsEndpoint endpoint) {
            var handler = new JsonPostHandler();
            using var clientX = new ClientX(endpoint);
            var httpClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            InjectClient(clientX, httpClient);

            var response = await clientX.Resolve("evotec.pl", DnsRecordType.A, retryOnTransient: false);

            Assert.Equal(HttpMethod.Post, handler.Request?.Method);
            Assert.Equal("application/json", handler.Request?.Content?.Headers.ContentType?.MediaType);
            Assert.NotEmpty(response.Answers);
        }
    }
}
