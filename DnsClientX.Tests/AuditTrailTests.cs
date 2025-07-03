using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class AuditTrailTests {
        [Fact]
        public async Task ShouldNotRecordWhenDisabled() {
            using var client = new ClientX();
            var response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            Assert.Empty(client.AuditTrail);
        }

        [Fact]
        public async Task ShouldRecordResponseWhenEnabled() {
            using var client = new ClientX { EnableAudit = true };
            var response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            Assert.Single(client.AuditTrail);
            var entry = Assert.Single(client.AuditTrail);
            Assert.Equal("example.com", entry.Name);
            Assert.Equal(DnsRecordType.A, entry.RecordType);
            Assert.Same(response, entry.Response);
            Assert.Null(entry.Exception);
        }

        private class ThrowingHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                throw new HttpRequestException("network error");
            }
        }

        [Fact]
        public async Task ShouldRecordException() {
            var handler = new ThrowingHandler();
            using var client = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps) { EnableAudit = true };
            var customClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(client)!;
            clients[client.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(client, customClient);

            var response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            var entry = Assert.Single(client.AuditTrail);
            Assert.NotNull(entry.Exception);
            Assert.Null(entry.Response);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
        }
    }
}
