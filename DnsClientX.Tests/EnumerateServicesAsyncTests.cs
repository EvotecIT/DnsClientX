using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class EnumerateServicesAsyncTests {
        [Fact]
        public async Task ShouldStreamServices() {
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

            var results = new List<DnsService>();
            await foreach (var r in client.EnumerateServicesAsync("example.com", CancellationToken.None)) {
                results.Add(r);
            }

            Assert.Single(results);
            var service = results[0];
            Assert.Equal("_http._tcp.example.com", service.ServiceName);
            Assert.Equal("host.example.com", service.Target);
            Assert.Equal(80, service.Port);
            Assert.True(service.Metadata != null && service.Metadata["path"] == "/");
        }
    }
}
