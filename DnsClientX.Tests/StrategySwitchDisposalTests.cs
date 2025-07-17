using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
/// <summary>
/// Tests disposal counts when switching between DNS strategies.
/// </summary>
[Collection("DisposalTests")]
public class StrategySwitchDisposalTests {
        private class JsonResponseHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"Status\":0}") };
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
        /// Ensures each strategy switch increments the disposal counter.
        /// </summary>
        [Fact]
        public async Task MultipleStrategySwitches_ShouldIncreaseDisposalCount() {
            var initialCount = ClientX.DisposalCount;
            var handler = new JsonResponseHandler();
            using (var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON)) {
                var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
                InjectClient(client, httpClient);

                await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
                client.EndpointConfiguration.SelectionStrategy = DnsSelectionStrategy.Random;
                await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
                client.EndpointConfiguration.SelectionStrategy = DnsSelectionStrategy.Failover;
                await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            }

            // Wait a moment for any async disposal to complete
            await Task.Delay(50);

            var finalCount = ClientX.DisposalCount;
            var diff = finalCount - initialCount;
            Assert.True(diff >= 3 && diff <= 6,
                $"Expected 3-6 disposals but was {diff}");
        }
    }
}
