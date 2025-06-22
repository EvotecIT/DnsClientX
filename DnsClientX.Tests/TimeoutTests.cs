using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class TimeoutTests {
        private class DelayingHandler : HttpMessageHandler {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                await Task.Delay(500, cancellationToken);
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) };
            }
        }

        [Fact]
        public async Task Resolve_ShouldFailWithTimeout() {
            var handler = new DelayingHandler();
            var clientX = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps, timeOutMilliseconds: 100);

            var customClient = new HttpClient(handler) {
                BaseAddress = clientX.EndpointConfiguration.BaseUri,
                Timeout = TimeSpan.FromMilliseconds(clientX.EndpointConfiguration.TimeOut)
            };

            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            var response = await clientX.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        }
    }
}
