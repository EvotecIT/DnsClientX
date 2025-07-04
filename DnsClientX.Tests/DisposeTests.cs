using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DisposeTests {
        private class TrackingHandler : HttpMessageHandler {
            public int DisposeCount { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                DisposeCount++;
            }
        }

        [Fact]
        public void Client_Dispose_ShouldNotDisposeHttpClientTwice() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            clientX.Dispose();

            Assert.Equal(1, handler.DisposeCount);
        }

        [Fact]
        public async Task Client_DisposeAsync_ShouldNotDisposeHttpClientTwice() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            await using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            await clientX.DisposeAsync();

            Assert.Equal(1, handler.DisposeCount);
        }

        [Fact]
        public async Task Client_Dispose_CalledConcurrently_ShouldOnlyDisposeOnce() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++) {
                tasks.Add(Task.Run(() => clientX.Dispose()));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(1, handler.DisposeCount);
        }

        [Fact]
        public async Task Client_DisposeAsync_CalledConcurrently_ShouldOnlyDisposeOnce() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++) {
                tasks.Add(Task.Run(() => clientX.DisposeAsync().AsTask()));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(1, handler.DisposeCount);
        }
    }
}
