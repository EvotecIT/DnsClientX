using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests ensuring proper disposal of internal resources.
    /// </summary>
    public class DisposeTests {
        private class TrackingHandler : HttpClientHandler {
            public int DisposeCount { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                DisposeCount++;
            }
        }

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        private class TrackingAsyncHandler : HttpClientHandler, IAsyncDisposable {
            public int DisposeCount { get; private set; }
            public int DisposeAsyncCount { get; private set; }

            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                DisposeCount++;
            }

            ValueTask IAsyncDisposable.DisposeAsync() {
                DisposeAsyncCount++;
                return ValueTask.CompletedTask;
            }
        }
#endif

        /// <summary>
        /// Ensures that calling <see cref="ClientX.Dispose"/> does not dispose the underlying <see cref="HttpClient"/> twice.
        /// </summary>
        [Fact]
        public void Client_Dispose_ShouldNotDisposeHttpClientTwice() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://example.com") };
            using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            handlerField.SetValue(clientX, handler);
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, false);
            Assert.Same(handler, handlerField.GetValue(clientX));

            clientX.Dispose();

            Assert.Equal(1, handler.DisposeCount);
        }

        /// <summary>
        /// Verifies that disposing <see cref="ClientX"/> does not dispose the HTTP handler multiple times.
        /// </summary>
        [Fact]
        public void Client_Dispose_ShouldNotDisposeHandlerTwice() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            handlerField.SetValue(clientX, handler);
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, true);

            clientX.Dispose();

            Assert.Equal(1, handler.DisposeCount);
        }

        /// <summary>
        /// Ensures the asynchronous dispose method does not dispose the <see cref="HttpClient"/> twice.
        /// </summary>
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
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, true);

            await clientX.DisposeAsync();

            Assert.Equal(1, handler.DisposeCount);
        }

        /// <summary>
        /// Checks that <see cref="ClientX.DisposeAsync"/> does not dispose the HTTP handler more than once.
        /// </summary>
        [Fact]
        public async Task Client_DisposeAsync_ShouldNotDisposeHandlerTwice() {
            var handler = new TrackingHandler();
            var customClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
            await using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            handlerField.SetValue(clientX, handler);
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, true);

            await clientX.DisposeAsync();

            Assert.Equal(1, handler.DisposeCount);
        }

        /// <summary>
        /// Ensures that concurrent calls to <see cref="ClientX.Dispose"/> only dispose once.
        /// </summary>
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
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, true);

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++) {
                tasks.Add(Task.Run(() => clientX.Dispose()));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(1, handler.DisposeCount);
        }

        /// <summary>
        /// Ensures concurrent invocations of <see cref="ClientX.DisposeAsync"/> only dispose once.
        /// </summary>
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
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, true);

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++) {
                tasks.Add(Task.Run(() => clientX.DisposeAsync().AsTask()));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(1, handler.DisposeCount);
        }

        /// <summary>
        /// Validates that the disposal counter is incremented when <see cref="ClientX.DisposeAsync"/> is called.
        /// </summary>
        [Fact]
        public async Task Client_DisposeAsync_ShouldIncrementDisposalCount() {
            ClientX.DisposalCount = 0;
            await using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);

            await clientX.DisposeAsync();

            Assert.Equal(1, ClientX.DisposalCount);
        }

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        /// <summary>
        /// Tests that asynchronous disposal prefers calling <see cref="IAsyncDisposable.DisposeAsync"/> on the handler when available.
        /// </summary>
        [Fact]
        public async Task Client_DisposeAsync_ShouldPreferAsyncHandlerDisposal() {
            var handler = new TrackingAsyncHandler();
            var customClient = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://example.com") };
            await using var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);
            var handlerField = typeof(ClientX).GetField("handler", BindingFlags.NonPublic | BindingFlags.Instance)!;
            handlerField.SetValue(clientX, handler);
            typeof(ClientX).GetField("_handlerOwnedByClient", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(clientX, false);

            await clientX.DisposeAsync();

            Assert.True(handler.DisposeAsyncCount >= 1, $"AsyncCount={handler.DisposeAsyncCount} DisposeCount={handler.DisposeCount}");
            Assert.True(handler.DisposeAsyncCount >= handler.DisposeCount, $"AsyncCount={handler.DisposeAsyncCount} DisposeCount={handler.DisposeCount}");
        }
#endif

        /// <summary>
        /// Ensures the list tracking disposed clients is cleared upon disposal.
        /// </summary>
        [Fact]
        public void Client_Dispose_ShouldClearDisposedClientsList() {
            var clientX = new ClientX("example.com", DnsRequestFormat.DnsOverHttps);

            clientX.Dispose();

            var field = typeof(ClientX).GetField("_disposedClients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var disposedClients = (HashSet<object>)field.GetValue(clientX)!;
            Assert.Empty(disposedClients);
        }
    }
}
