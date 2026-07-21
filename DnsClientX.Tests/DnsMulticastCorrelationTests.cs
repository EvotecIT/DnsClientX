using System.Net;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests mDNS response correlation independently of multicast socket timing.</summary>
    public class DnsMulticastCorrelationTests {
        /// <summary>Unrelated announcements are not retained merely because mDNS uses transaction ID zero.</summary>
        [Fact]
        public void RejectsUnrelatedAnnouncement() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = [new DnsAnswer {
                    Name = "printer.local",
                    Type = DnsRecordType.A,
                    DataRaw = "192.0.2.20"
                }]
            };

            Assert.False(DnsWireResolveMulticast.IsRelevantResponse(response, "server.local", DnsRecordType.A));
        }

        /// <summary>A response remains relevant when the requested owner reaches the type through an alias.</summary>
        [Fact]
        public void AcceptsRequestedAliasPath() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = [
                    new DnsAnswer {
                        Name = "service.local",
                        Type = DnsRecordType.CNAME,
                        DataRaw = "target.local."
                    },
                    new DnsAnswer {
                        Name = "target.local",
                        Type = DnsRecordType.A,
                        DataRaw = "192.0.2.21"
                    }
                ]
            };

            Assert.True(DnsWireResolveMulticast.IsRelevantResponse(response, "service.local", DnsRecordType.A));
        }

        /// <summary>Only DNS-SD records reachable from the correlated PTR answer are retained.</summary>
        [Fact]
        public void RetainsOnlyRelatedDnsSdRecords() {
            var response = new DnsResponse {
                Status = DnsResponseCode.NoError,
                Answers = [new DnsAnswer {
                    Name = "_http._tcp.local",
                    Type = DnsRecordType.PTR,
                    DataRaw = "Site._http._tcp.local."
                }],
                Additional = [
                    new DnsAnswer {
                        Name = "Site._http._tcp.local",
                        Type = DnsRecordType.SRV,
                        DataRaw = "0 0 443 host.local."
                    },
                    new DnsAnswer {
                        Name = "host.local",
                        Type = DnsRecordType.A,
                        DataRaw = "192.0.2.25"
                    },
                    new DnsAnswer {
                        Name = "attacker.local",
                        Type = DnsRecordType.A,
                        DataRaw = "192.0.2.66"
                    }
                ]
            };

            DnsWireResolveMulticast.RetainRelevantRecords(response, "_http._tcp.local", DnsRecordType.PTR);

            Assert.Equal(2, response.Additional.Length);
            Assert.DoesNotContain(response.Additional, item => item.Name == "attacker.local");
        }

        /// <summary>IPv6 link-local multicast destinations carry the selected interface scope.</summary>
        [Fact]
        public void ScopesIpv6TargetToSelectedInterface() {
            IPEndPoint target = DnsWireResolveMulticast.CreateTargetEndPoint(
                IPAddress.Parse("ff02::fb"),
                5353,
                17);

            Assert.Equal(17, target.Address.ScopeId);
            Assert.Equal(5353, target.Port);
        }

        /// <summary>IPv4 multicast destinations are not rewritten with an IPv6 scope.</summary>
        [Fact]
        public void LeavesIpv4TargetUnscoped() {
            IPAddress address = IPAddress.Parse("224.0.0.251");
            IPEndPoint target = DnsWireResolveMulticast.CreateTargetEndPoint(address, 5353, 17);

            Assert.Equal(address, target.Address);
            Assert.Equal(5353, target.Port);
        }
    }
}
