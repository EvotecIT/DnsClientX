using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests operating-system DNS search candidate construction.
    /// </summary>
    public class SystemDnsConfigurationTests {
        /// <summary>Searches suffixes before an absolute query when the name has fewer dots than ndots.</summary>
        [Fact]
        public void BuildsSearchFirstCandidates() {
            var configuration = new SystemDnsConfiguration(
                new[] { "192.0.2.53" },
                new[] { "corp.example", "example.test" },
                2,
                SystemDnsDiscoverySource.CustomProvider);

            Assert.Equal(
                new[] { "host.corp.example", "host.example.test", "host" },
                configuration.BuildQueryCandidates("host"));
        }

        /// <summary>Queries a sufficiently qualified name before applying search suffixes.</summary>
        [Fact]
        public void BuildsAbsoluteFirstCandidates() {
            var configuration = new SystemDnsConfiguration(
                Array.Empty<string>(),
                new[] { "corp.example" },
                1,
                SystemDnsDiscoverySource.CustomProvider);

            Assert.Equal(
                new[] { "host.example", "host.example.corp.example" },
                configuration.BuildQueryCandidates("host.example"));
        }

        /// <summary>A trailing root label suppresses search-list processing.</summary>
        [Fact]
        public void PreservesExplicitAbsoluteName() {
            var configuration = new SystemDnsConfiguration(
                Array.Empty<string>(),
                new[] { "corp.example" },
                1,
                SystemDnsDiscoverySource.CustomProvider);

            Assert.Equal(new[] { "host." }, configuration.BuildQueryCandidates("host."));
        }

        /// <summary>NODATA advances through the search list instead of terminating the lookup.</summary>
        [Fact]
        public async Task SearchContinuesAfterNoData() {
            var endpoint = new Configuration("192.0.2.53", DnsRequestFormat.DnsOverUDP);
            typeof(Configuration).GetProperty(nameof(Configuration.SystemDnsConfiguration))!
                .SetValue(endpoint, new SystemDnsConfiguration(
                    new[] { "192.0.2.53" },
                    new[] { "corp.example", "example.test" },
                    2,
                    SystemDnsDiscoverySource.CustomProvider));
            var queried = new List<string>();
            using var client = new ClientX(endpoint);
            client.ResolverOverride = (candidate, _, _) => {
                queried.Add(candidate);
                return Task.FromResult(new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = candidate == "host.example.test"
                        ? [new DnsAnswer { Name = candidate, Type = DnsRecordType.A, DataRaw = "192.0.2.44" }]
                        : Array.Empty<DnsAnswer>()
                });
            };

            DnsResponse response = await client.Resolve("host", retryOnTransient: false);

            Assert.Equal(new[] { "host.corp.example", "host.example.test" }, queried);
            Assert.Single(response.Answers);
        }

        /// <summary>A projected CNAME result remains terminal for the successful search candidate.</summary>
        [Fact]
        public async Task SearchStopsAfterProjectedAliasAnswer() {
            var endpoint = new Configuration("192.0.2.53", DnsRequestFormat.DnsOverUDP);
            typeof(Configuration).GetProperty(nameof(Configuration.SystemDnsConfiguration))!
                .SetValue(endpoint, new SystemDnsConfiguration(
                    new[] { "192.0.2.53" },
                    new[] { "corp.example", "example.test" },
                    2,
                    SystemDnsDiscoverySource.CustomProvider));
            var queried = new List<string>();
            using var client = new ClientX(endpoint);
            client.ResolverOverride = (candidate, _, _) => {
                queried.Add(candidate);
                var response = new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = candidate == "host.corp.example"
                        ? [
                            new DnsAnswer { Name = candidate, Type = DnsRecordType.CNAME, DataRaw = "target.example." },
                            new DnsAnswer { Name = "target.example", Type = DnsRecordType.A, DataRaw = "192.0.2.45" }
                        ]
                        : [new DnsAnswer { Name = candidate, Type = DnsRecordType.A, DataRaw = "192.0.2.99" }]
                };
                ClientX.ApplyAnswerProjection(response, candidate, DnsRecordType.A, returnAllTypes: false);
                return Task.FromResult(response);
            };

            DnsResponse response = await client.Resolve("host", retryOnTransient: false);

            Assert.Equal(new[] { "host.corp.example" }, queried);
            DnsAnswer answer = Assert.Single(response.Answers);
            Assert.Equal("target.example", answer.Name);
            Assert.Equal("192.0.2.45", answer.DataRaw);
        }
    }
}
