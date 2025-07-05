using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class CancellationTests {
        private class DelayingHandler : HttpMessageHandler {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                await Task.Delay(5000, cancellationToken);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
            }
        }

        [Fact]
        public async Task ResolveShouldCancelEarly() {
            var handler = new DelayingHandler();
            using var clientX = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);

            var customClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            using var cts = new CancellationTokenSource(100);
            await Assert.ThrowsAsync<TaskCanceledException>(() => clientX.Resolve("example.com", DnsRecordType.A, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task QueryDnsShouldCancelEarly() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => ClientX.QueryDns("example.com", DnsRecordType.A, cancellationToken: cts.Token));
        }

        [Fact]
        public async Task QueryDns_ShouldDisposeClient_WhenCancelled() {
            ClientX.DisposalCount = 0;
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => ClientX.QueryDns("example.com", DnsRecordType.A, cancellationToken: cts.Token));

            Assert.Equal(1, ClientX.DisposalCount);
        }
    }
}
