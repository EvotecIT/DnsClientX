using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared execution-target client creation helpers.
    /// </summary>
    public class ResolverExecutionClientFactoryTests {
        /// <summary>
        /// Ensures built-in execution targets create runnable clients.
        /// </summary>
        [Fact]
        public void CreateClient_CreatesBuiltInClient() {
            using var client = ResolverExecutionClientFactory.CreateClient(new ResolverExecutionTarget {
                DisplayName = "Cloudflare",
                BuiltInEndpoint = DnsEndpoint.Cloudflare
            });

            Assert.Equal(DnsRequestFormat.DnsOverHttpsJSON, client.EndpointConfiguration.RequestFormat);
        }

        /// <summary>
        /// Ensures optional port overrides are applied to created clients.
        /// </summary>
        [Fact]
        public void CreateClient_AppliesPortOverride() {
            using var client = ResolverExecutionClientFactory.CreateClient(
                new ResolverExecutionTarget {
                    DisplayName = "tcp@1.1.1.1:53",
                    ExplicitEndpoint = new DnsResolverEndpoint {
                        Transport = Transport.Tcp,
                        Host = "1.1.1.1",
                        Port = 53
                    }
                },
                new ResolverExecutionClientOptions {
                    PortOverride = 5353
                });

            Assert.Equal(5353, client.EndpointConfiguration.Port);
        }

        /// <summary>
        /// Ensures shared client options apply audit, timeout, and DoH wire-post preferences.
        /// </summary>
        [Fact]
        public void CreateClient_AppliesSharedClientOptions() {
            using var client = ResolverExecutionClientFactory.CreateClient(
                new ResolverExecutionTarget {
                    DisplayName = "Cloudflare",
                    BuiltInEndpoint = DnsEndpoint.Cloudflare
                },
                new ResolverExecutionClientOptions {
                    EnableAudit = true,
                    TimeoutMs = 2345,
                    ForceDohWirePost = true
                });

            Assert.True(client.EnableAudit);
            Assert.Equal(2345, client.EndpointConfiguration.TimeOut);
            Assert.Equal(DnsRequestFormat.DnsOverHttpsWirePost, client.EndpointConfiguration.RequestFormat);
        }

        /// <summary>
        /// Ensures invalid targets are rejected.
        /// </summary>
        [Fact]
        public void CreateClient_RejectsInvalidTarget() {
            Assert.Throws<InvalidOperationException>(() => ResolverExecutionClientFactory.CreateClient(new ResolverExecutionTarget {
                DisplayName = "invalid"
            }));
        }
    }
}
