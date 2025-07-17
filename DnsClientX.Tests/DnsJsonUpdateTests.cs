using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for DNS record updates via JSON API.
    /// </summary>
    public class DnsJsonUpdateTests {
        private class JsonUpdateHandler : HttpMessageHandler {
            public HttpRequestMessage? Request { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Request = request;
                var json = "{\"Status\":0}";
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

        /// <summary>
        /// Verifies that an update request is posted using JSON.
        /// </summary>
        [Fact]
        public async Task UpdateRecordJsonPost_Add() {
            var handler = new JsonUpdateHandler();
            using var clientX = new ClientX(DnsEndpoint.CloudflareJsonPost);
            var httpClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            InjectClient(clientX, httpClient);

            var response = await clientX.UpdateRecordAsync("example.com", "www.example.com", DnsRecordType.A, "1.2.3.4");

            Assert.Equal(HttpMethod.Post, handler.Request?.Method);
            Assert.Equal("application/json", handler.Request?.Content?.Headers.ContentType?.MediaType);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
        }

        /// <summary>
        /// Verifies that deleting a record uses a JSON POST request.
        /// </summary>
        [Fact]
        public async Task DeleteRecordJsonPost_Delete() {
            var handler = new JsonUpdateHandler();
            using var clientX = new ClientX(DnsEndpoint.CloudflareJsonPost);
            var httpClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            InjectClient(clientX, httpClient);

            var response = await clientX.DeleteRecordAsync("example.com", "www.example.com", DnsRecordType.A);

            Assert.Equal(HttpMethod.Post, handler.Request?.Method);
            Assert.Equal("application/json", handler.Request?.Content?.Headers.ContentType?.MediaType);
            Assert.Equal(DnsResponseCode.NoError, response.Status);
        }
    }
}
