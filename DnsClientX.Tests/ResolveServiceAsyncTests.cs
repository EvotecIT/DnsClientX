using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class ResolveServiceAsyncTests : NetworkTestBase {
        [Fact]
        public async Task ShouldOrderByPriorityAndWeight() {
            var srvResponse = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "_http._tcp.example.com", Type = DnsRecordType.SRV, DataRaw = "10 5 80 host1.example.com." },
                    new DnsAnswer { Name = "_http._tcp.example.com", Type = DnsRecordType.SRV, DataRaw = "10 10 80 host2.example.com." },
                    new DnsAnswer { Name = "_http._tcp.example.com", Type = DnsRecordType.SRV, DataRaw = "5 1 80 host3.example.com." }
                }
            };

            using var client = new ClientX(DnsEndpoint.System);
            client.ResolverOverride = (name, type, ct) => Task.FromResult(srvResponse);

            var records = await client.ResolveServiceAsync("http", "tcp", "example.com");
            Assert.Equal(3, records.Length);
            Assert.Equal("host3.example.com", records[0].Target); // priority 5 first
            Assert.Equal("host2.example.com", records[1].Target); // higher weight within same priority
            Assert.Equal("host1.example.com", records[2].Target);
        }

        [Fact]
        public async Task ShouldResolveHostAddresses() {
            var srvResponse = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "_ldap._tcp.example.com", Type = DnsRecordType.SRV, DataRaw = "0 0 389 host.example.com." }
                }
            };
            var aResponse = new DnsResponse {
                Answers = new[] { new DnsAnswer { Name = "host.example.com", Type = DnsRecordType.A, DataRaw = "10.0.0.1" } }
            };
            var aaaaResponse = new DnsResponse {
                Answers = new[] { new DnsAnswer { Name = "host.example.com", Type = DnsRecordType.AAAA, DataRaw = "::1" } }
            };

            using var client = new ClientX(DnsEndpoint.System);
            client.ResolverOverride = (name, type, ct) => {
                if (name == "_ldap._tcp.example.com" && type == DnsRecordType.SRV) return Task.FromResult(srvResponse);
                if (name == "host.example.com" && type == DnsRecordType.A) return Task.FromResult(aResponse);
                if (name == "host.example.com" && type == DnsRecordType.AAAA) return Task.FromResult(aaaaResponse);
                return Task.FromResult(new DnsResponse { Answers = Array.Empty<DnsAnswer>() });
            };

            var records = await client.ResolveServiceAsync("ldap", "tcp", "example.com", resolveHosts: true);
            Assert.Single(records);
            var r = records[0];
            Assert.NotNull(r.Addresses);
            Assert.Contains(IPAddress.Parse("10.0.0.1"), r.Addresses!);
            Assert.Contains(IPAddress.Parse("::1"), r.Addresses!);
        }
    }
}
