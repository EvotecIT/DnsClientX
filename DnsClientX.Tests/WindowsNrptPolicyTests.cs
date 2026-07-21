using System;
using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Validates dependency-free Windows NRPT discovery and policy matching.</summary>
    [Collection("NoParallel")]
    public sealed class WindowsNrptPolicyTests : IDisposable {
        /// <summary>Configures isolated system DNS discovery for each test.</summary>
        public WindowsNrptPolicyTests() {
            SystemInformation.SetDnsServerProvider(() => new List<string> { "192.0.2.53" });
            SystemInformation.SetDnsPolicyProvider(null);
        }

        /// <summary>Generic DNS and DNSSEC options are parsed from their documented registry values.</summary>
        [Fact]
        public void ParsesGenericResolversAndDnsSecRequirement() {
            SystemDnsPolicyRule rule = WindowsNrptPolicyReader.ParseRule(
                "rule-1",
                SystemDnsPolicySource.GroupPolicy,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) {
                    ["Name"] = new[] { ".secure.example" },
                    ["ConfigOptions"] = 0x0A,
                    ["GenericDNSServers"] = "192.0.2.10; 2001:db8::53",
                    ["DNSSECValidationRequired"] = 1
                });

            Assert.True(rule.IsSupported);
            Assert.True(rule.DnsSecValidationRequired);
            Assert.Equal(new[] { "192.0.2.10", "2001:db8::53" }, rule.NameServers);
        }

        /// <summary>FQDN policy wins over prefix, suffix, and catch-all matches.</summary>
        [Fact]
        public void SelectsMostSpecificNamespaceKind() {
            var configuration = CreateConfiguration(
                Rule("any", ".", "192.0.2.1"),
                Rule("suffix", ".example", "192.0.2.2"),
                Rule("prefix", "host", "192.0.2.3"),
                Rule("fqdn", "host.example", "192.0.2.4"));

            SystemDnsPolicyMatch match = Assert.IsType<SystemDnsPolicyMatch>(configuration.MatchPolicy("HOST.EXAMPLE."));

            Assert.Equal("fqdn", match.RuleId);
            Assert.Equal("192.0.2.4", Assert.Single(match.NameServers));
        }

        /// <summary>A suffix policy matches its namespace apex and descendants only.</summary>
        [Fact]
        public void SuffixIncludesNamespaceApexAndChildren() {
            var configuration = CreateConfiguration(Rule("suffix", ".corp.example", "192.0.2.8"));

            Assert.Equal("suffix", configuration.MatchPolicy("corp.example")?.RuleId);
            Assert.Equal("suffix", configuration.MatchPolicy("dc1.eu.corp.example")?.RuleId);
            Assert.Null(configuration.MatchPolicy("notcorp.example"));
        }

        /// <summary>Conflicting rules for an identical namespace are rejected.</summary>
        [Fact]
        public void ConflictingSameNamespaceRulesAreNotApplied() {
            var configuration = CreateConfiguration(
                Rule("first", ".corp.example", "192.0.2.8"),
                Rule("second", ".corp.example", "192.0.2.9"));

            SystemDnsPolicyMatch match = Assert.IsType<SystemDnsPolicyMatch>(configuration.MatchPolicy("host.corp.example"));

            Assert.False(match.CanApply);
            Assert.Contains("Conflicting NRPT rules", match.Diagnostic);
        }

        /// <summary>A system query snapshot selects the policy-specific resolver.</summary>
        [Fact]
        public void SystemQuerySnapshotUsesPolicyResolver() {
            SystemInformation.SetDnsPolicyProvider(() => new SystemDnsPolicyDiscoveryResult(new[] {
                Rule("route", ".corp.example", "192.0.2.99")
            }));
            var endpoint = new Configuration(DnsEndpoint.System);

            Configuration snapshot = endpoint.CreateQuerySnapshot("host.corp.example");

            Assert.Equal("192.0.2.99", snapshot.Hostname);
            Assert.Equal("route", snapshot.AppliedSystemDnsPolicy?.RuleId);
            Assert.Equal(DnsRequestFormat.DnsOverUDP, snapshot.RequestFormat);
        }

        /// <summary>DirectAccess behavior is surfaced instead of being simulated.</summary>
        [Fact]
        public void DirectAccessRuleIsReportedUnsupported() {
            SystemDnsPolicyRule rule = WindowsNrptPolicyReader.ParseRule(
                "direct-access",
                SystemDnsPolicySource.Local,
                new Dictionary<string, object?> {
                    ["Name"] = new[] { ".corp.example" },
                    ["ConfigOptions"] = 0x0C,
                    ["GenericDNSServers"] = "192.0.2.10"
                });

            Assert.False(rule.IsSupported);
            Assert.Contains("DirectAccess", rule.Diagnostic);
        }

        /// <summary>Restores process-wide test discovery hooks.</summary>
        public void Dispose() {
            SystemInformation.SetDnsPolicyProvider(null);
            SystemInformation.SetDnsServerProvider(null);
        }

        private static SystemDnsConfiguration CreateConfiguration(params SystemDnsPolicyRule[] rules) {
            return new SystemDnsConfiguration(
                new[] { "192.0.2.53" },
                Array.Empty<string>(),
                1,
                SystemDnsDiscoverySource.CustomProvider,
                policyRules: rules);
        }

        private static SystemDnsPolicyRule Rule(string id, string dnsNamespace, string server) {
            return new SystemDnsPolicyRule(
                id,
                SystemDnsPolicySource.Local,
                new[] { dnsNamespace },
                new[] { server },
                dnsSecValidationRequired: false,
                isSupported: true,
                diagnostic: null);
        }
    }
}
