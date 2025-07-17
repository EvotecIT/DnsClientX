using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="ClientX.ResolveServiceAsync"/> helper.
    /// </summary>
    public class ResolveServiceAsyncTests {
        /// <summary>
        /// Ensures SRV records are ordered by priority and weight as defined in the specification.
        /// </summary>
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

        /// <summary>
        /// Resolves host addresses from SRV records using the helper method.
        /// </summary>
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

        /// <summary>
        /// Returns an empty collection when the DNS server provides no SRV records.
        /// </summary>
        [Fact]
        public async Task ShouldReturnEmptyArrayWhenNoSrvRecords() {
            using var client = new ClientX(DnsEndpoint.System);
            client.ResolverOverride = (name, type, ct) =>
                Task.FromException<DnsResponse>(new DnsClientException(
                    "not found",
                    new DnsResponse { Status = DnsResponseCode.NXDomain }));

            var records = await client.ResolveServiceAsync("http", "tcp", "example.com");
            Assert.Empty(records);
        }
    }
}
