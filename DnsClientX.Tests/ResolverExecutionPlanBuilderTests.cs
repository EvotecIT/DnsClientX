using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests normalized resolver execution target planning.
    /// </summary>
    public class ResolverExecutionPlanBuilderTests {
        /// <summary>
        /// Ensures built-in selections normalize to a single built-in execution target.
        /// </summary>
        [Fact]
        public void BuildSelectionTarget_BuiltInSelection_ReturnsBuiltInTarget() {
            ResolverExecutionTarget target = ResolverExecutionPlanBuilder.BuildSelectionTarget(new ResolverSelectionResult {
                Kind = ResolverSelectionKind.BuiltInEndpoint,
                Target = "Cloudflare",
                BuiltInEndpoint = DnsEndpoint.Cloudflare
            });

            Assert.Equal("Cloudflare", target.DisplayName);
            Assert.Equal(DnsEndpoint.Cloudflare, target.BuiltInEndpoint);
            Assert.Null(target.ExplicitEndpoint);
        }

        /// <summary>
        /// Ensures explicit selections normalize to a single explicit execution target.
        /// </summary>
        [Fact]
        public void BuildSelectionTarget_ExplicitSelection_ReturnsExplicitTarget() {
            var endpoint = new DnsResolverEndpoint {
                Transport = Transport.Doh,
                DohUrl = new Uri("https://dns.example/dns-query")
            };

            ResolverExecutionTarget target = ResolverExecutionPlanBuilder.BuildSelectionTarget(new ResolverSelectionResult {
                Kind = ResolverSelectionKind.ExplicitEndpoint,
                Target = "custom",
                ExplicitEndpoint = endpoint
            });

            Assert.Equal("doh@https://dns.example/dns-query", target.DisplayName);
            Assert.Equal(endpoint, target.ExplicitEndpoint);
            Assert.Null(target.BuiltInEndpoint);
        }

        /// <summary>
        /// Ensures explicit endpoint planning removes duplicate targets by display name.
        /// </summary>
        [Fact]
        public void BuildExplicitTargets_DeduplicatesEquivalentTargets() {
            ResolverExecutionTarget[] targets = ResolverExecutionPlanBuilder.BuildExplicitTargets(new[] {
                new DnsResolverEndpoint {
                    Transport = Transport.Tcp,
                    Host = "1.1.1.1",
                    Port = 53
                },
                new DnsResolverEndpoint {
                    Transport = Transport.Tcp,
                    Host = "1.1.1.1",
                    Port = 53
                }
            });

            Assert.Single(targets);
            Assert.Equal("tcp@1.1.1.1:53", targets[0].DisplayName);
        }

        /// <summary>
        /// Ensures probe planning expands composite built-in profiles through the shared builder.
        /// </summary>
        [Fact]
        public void BuildProbeTargets_SystemProfile_ExpandsExpectedTargets() {
            ResolverExecutionTarget[] targets = ResolverExecutionPlanBuilder.BuildProbeTargets(DnsEndpoint.System);

            Assert.Equal(2, targets.Length);
            Assert.Contains(targets, target => target.BuiltInEndpoint == DnsEndpoint.System);
            Assert.Contains(targets, target => target.BuiltInEndpoint == DnsEndpoint.SystemTcp);
        }

        /// <summary>
        /// Ensures endpoint descriptions honor explicit request format overrides.
        /// </summary>
        [Fact]
        public void DescribeEndpoint_UsesEffectiveRequestFormatTransport() {
            string description = ResolverExecutionPlanBuilder.DescribeEndpoint(new DnsResolverEndpoint {
                Transport = Transport.Udp,
                Host = "resolver.example",
                Port = 853,
                RequestFormat = DnsRequestFormat.DnsOverTLS
            });

            Assert.Equal("dot@resolver.example:853", description);
        }
    }
}
