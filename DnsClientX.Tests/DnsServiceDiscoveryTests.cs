using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsServiceDiscoveryTests {
        private class StubHandler : HttpMessageHandler {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
                string name = string.Empty;
                string type = "A";
                foreach (var part in request.RequestUri!.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)) {
                    var kv = part.Split('=', 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "name") name = Uri.UnescapeDataString(kv[1]);
                    if (kv[0] == "type") type = Uri.UnescapeDataString(kv[1]);
                }
                string json = "{}";
                if (name == "_services._dns-sd._udp.test" && type == "PTR") {
                    json = "{\"Status\":0,\"Answer\":[{\"name\":\"_services._dns-sd._udp.test\",\"type\":12,\"TTL\":60,\"data\":\"_http._tcp.test.\"}]}";
                } else if (name == "_http._tcp.test" && type == "SRV") {
                    json = "{\"Status\":0,\"Answer\":[{\"name\":\"_http._tcp.test\",\"type\":33,\"TTL\":60,\"data\":\"0 0 80 server.test.\"}]}";
                } else if (name == "_http._tcp.test" && type == "TXT") {
                    json = "{\"Status\":0,\"Answer\":[{\"name\":\"_http._tcp.test\",\"type\":16,\"TTL\":60,\"data\":\"path=/\"}]}";
                }
                var resp = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
                return Task.FromResult(resp);
            }
        }

        [Fact]
        public async Task ShouldDiscoverService() {
            var handler = new StubHandler();
            using var client = new ClientX("https://example.com/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
            var custom = new HttpClient(handler) { BaseAddress = client.EndpointConfiguration.BaseUri };
            var clientsField = typeof(ClientX).GetField("_clients", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var clients = (Dictionary<DnsSelectionStrategy, HttpClient>)clientsField.GetValue(client)!;
            clients[client.EndpointConfiguration.SelectionStrategy] = custom;
            var clientField = typeof(ClientX).GetField("Client", BindingFlags.NonPublic | BindingFlags.Instance)!;
            clientField.SetValue(client, custom);

            var results = await client.DiscoverServices("test");
            Assert.Single(results);
            var sd = results[0];
            Assert.Equal("_http._tcp.test", sd.ServiceName);
            Assert.Equal("server.test", sd.Host);
            Assert.Equal(80, sd.Port);
            Assert.True(sd.Txt.ContainsKey("path"));
            Assert.Equal("/", sd.Txt["path"]);
        }
    }
}
