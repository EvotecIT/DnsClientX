using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for wildcard pattern expansion and resolution logic.
    /// </summary>
    public class ResolvePatternTests {
        private class CountingHandler : HttpMessageHandler {
            public int CallCount;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                Interlocked.Increment(ref CallCount);
                var response = new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent("{\"Status\":0}")
                };
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
        /// Ensures that patterns with wildcards are expanded and each name is queried.
        /// </summary>
        [Fact]
        public async Task ResolvePattern_ExpandsWildcards() {
            var handler = new CountingHandler();
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
            var httpClient = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            InjectClient(client, httpClient);

            var responses = await client.ResolvePattern("host[1-3].example.com", DnsRecordType.A, retryOnTransient: false);

            Assert.Equal(3, handler.CallCount);
            Assert.Equal(3, responses.Length);
        }

        /// <summary>
        /// Verifies brace expansion of multiple segments.
        /// </summary>
        [Fact]
        public void ExpandPattern_MultipleBraces() {
            string pattern = "srv{a,b}{1,2}.example.com";

            string[] names = ClientX.ExpandPattern(pattern).ToArray();

            Assert.Equal(new[] { "srva1.example.com", "srva2.example.com", "srvb1.example.com", "srvb2.example.com" }, names);
        }
    }
}
