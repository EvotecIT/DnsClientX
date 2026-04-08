namespace DnsClientX.Tests {
    /// <summary>
    /// Tests built-in probe plan expansion for resolver families.
    /// </summary>
    public class ProbePlanBuilderTests {
        /// <summary>
        /// Ensures system resolver probing expands to the UDP and TCP system variants.
        /// </summary>
        [Fact]
        public void BuildPlan_SystemProfile_ExpandsToSystemVariants() {
            DnsEndpoint[] plan = ProbePlanBuilder.BuildPlan(DnsEndpoint.System);

            Assert.Equal(new[] { DnsEndpoint.System, DnsEndpoint.SystemTcp }, plan);
        }

        /// <summary>
        /// Ensures cloud resolver probing expands to the full transport family.
        /// </summary>
        [Fact]
        public void BuildPlan_CloudflareProfile_ExpandsToAllCloudflareVariants() {
            DnsEndpoint[] plan = ProbePlanBuilder.BuildPlan(DnsEndpoint.CloudflareWireFormatPost);

            Assert.Equal(
                new[] {
                    DnsEndpoint.Cloudflare,
                    DnsEndpoint.CloudflareWireFormat,
                    DnsEndpoint.CloudflareWireFormatPost,
                    DnsEndpoint.CloudflareJsonPost,
                    DnsEndpoint.CloudflareQuic,
                    DnsEndpoint.CloudflareOdoh
                },
                plan);
        }

        /// <summary>
        /// Ensures profiles without grouped transport variants remain unchanged.
        /// </summary>
        [Fact]
        public void BuildPlan_SingleProfile_ReturnsOriginalEndpoint() {
            DnsEndpoint[] plan = ProbePlanBuilder.BuildPlan(DnsEndpoint.NextDNS);

            Assert.Equal(new[] { DnsEndpoint.NextDNS }, plan);
        }
    }
}
