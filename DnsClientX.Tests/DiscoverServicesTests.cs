using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DiscoverServicesTests {
        [Fact]
        public async Task ShouldParseResponses() {
            var ptrResponse = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "_services._dns-sd._udp.example.com", Type = DnsRecordType.PTR, DataRaw = "_http._tcp.example.com." }
                }
            };
            var srvResponse = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "_http._tcp.example.com", Type = DnsRecordType.SRV, DataRaw = "0 0 80 host.example.com." }
                }
            };
            var txtResponse = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "_http._tcp.example.com", Type = DnsRecordType.TXT, DataRaw = "path=/" }
                }
            };

            using var client = new ClientX(DnsEndpoint.System);
            client.ResolverOverride = (name, type, ct) => {
                if (name == "_services._dns-sd._udp.example.com" && type == DnsRecordType.PTR) return Task.FromResult(ptrResponse);
                if (name == "_http._tcp.example.com" && type == DnsRecordType.SRV) return Task.FromResult(srvResponse);
                if (name == "_http._tcp.example.com" && type == DnsRecordType.TXT) return Task.FromResult(txtResponse);
                return Task.FromResult(new DnsResponse { Answers = Array.Empty<DnsAnswer>() });
            };

            var results = await client.DiscoverServices("example.com", CancellationToken.None);
            Assert.Single(results);
            var r = results[0];
            Assert.Equal("_http._tcp.example.com", r.ServiceName);
            Assert.Equal("host.example.com", r.Target);
            Assert.Equal(80, r.Port);
            Assert.True(r.Metadata != null && r.Metadata["path"] == "/");
        }
    }
}
