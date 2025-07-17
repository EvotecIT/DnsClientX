using System;
using System.Net.Http;
using DnsClientX;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests that verify proper handling of HttpRequestException scenarios during DNS resolution.
    /// These tests simulate network failures to ensure the ClientX class degrades gracefully
    /// when real-world network issues occur (connectivity problems, timeouts, server errors, etc.).
    /// </summary>
    public class ResolveHttpRequestException {
        /// <summary>
        /// Custom HttpMessageHandler that always throws HttpRequestException to simulate network failures.
        /// This allows us to test error handling without relying on actual network conditions.
        /// </summary>
        private class ThrowingHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                throw new HttpRequestException("network error");
            }
        }
        /// <summary>
        /// Verifies that <see cref="ClientX"/> correctly handles <see cref="HttpRequestException"/> scenarios.
        /// </summary>
        /// <remarks>
        /// This test injects a custom <see cref="HttpClient"/> that always throws <see cref="HttpRequestException"/>
        /// to simulate real-world network failures.
        /// </remarks>
        [Fact]
        public async Task ShouldHandleHttpRequestExceptionWithoutInner() {
            var handler = new ThrowingHandler();
            using var clientX = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);

            var customClient = new HttpClient(handler) { BaseAddress = clientX.EndpointConfiguration.BaseUri };
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(clientX)!;
            clients[clientX.EndpointConfiguration.SelectionStrategy] = customClient;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(clientX, customClient);

            var response = await clientX.Resolve("example.com", DnsRecordType.A, retryOnTransient: false);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            Assert.Contains("network error", response.Error);
        }
    }
}
