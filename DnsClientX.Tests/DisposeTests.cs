using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DisposeTests {
        private class DummyHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }

        private class TestHttpClient : HttpClient {
            public bool Disposed { get; private set; }
            public TestHttpClient(HttpMessageHandler handler) : base(handler) {
            }
            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                Disposed = true;
            }
        }

        [Fact]
        public void DisposeShouldCleanupResources() {
            var handler = new DummyHandler();
            var clientX = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);

            var testClient = new TestHttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = testClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, testClient);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            handlerField.SetValue(clientX, handler);

            clientX.Dispose();

            Assert.True(testClient.Disposed);
            Assert.Empty(clients);
            Assert.Null(clientField.GetValue(clientX));
            Assert.Null(handlerField.GetValue(clientX));
        }

        [Fact]
        public void DisposeCanBeCalledMultipleTimes() {
            var clientX = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);
            clientX.Dispose();
            clientX.Dispose();
        }
    }
}
