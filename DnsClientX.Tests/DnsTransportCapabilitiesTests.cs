using System.Reflection;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared runtime transport capability reporting.
    /// </summary>
    public class DnsTransportCapabilitiesTests {
        /// <summary>Unsupported variants sharing a label are counted individually while repeated attempts count once.</summary>
        [Fact]
        public void UnsupportedCandidatesUseConfiguredIdentity() {
            var first = new ResolverQueryAttemptResult { Target = "same", CandidateId = "first", RequestFormat = (DnsRequestFormat)int.MaxValue };
            var second = new ResolverQueryAttemptResult { Target = "same", CandidateId = "second", RequestFormat = (DnsRequestFormat)int.MaxValue };
            var attempts = new[] { first, second, first };
            Assert.Equal(2, DnsTransportCapabilities.CountUnsupportedTargets(attempts));
            string[] warnings = DnsTransportCapabilities.GetUnsupportedWarnings(attempts);
            Assert.Equal(2, warnings.Length);
            Assert.Contains(warnings, warning => warning.Contains("(first)"));
            Assert.Contains(warnings, warning => warning.Contains("(second)"));
        }

        /// <summary>
        /// Ensures modern transport support matches the current target framework expectations.
        /// </summary>
        [Fact]
        public void Supports_ModernTransports_MatchesTargetFramework() {
#if NET8_0_OR_GREATER
            Assert.Equal(GetRuntimeQuicSupport(), DnsTransportCapabilities.SupportsDnsOverHttp3);
            Assert.Equal(GetRuntimeQuicSupport(), DnsTransportCapabilities.SupportsDnsOverQuic);
#else
            Assert.False(DnsTransportCapabilities.SupportsDnsOverHttp3);
            Assert.False(DnsTransportCapabilities.SupportsDnsOverQuic);
#endif
        }

        /// <summary>
        /// Ensures request-format support evaluation is aligned with the capability surface.
        /// </summary>
        [Fact]
        public void Supports_RequestFormat_UsesCapabilitySurface() {
            Assert.Equal(DnsTransportCapabilities.SupportsDnsOverHttp3, DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttp3));
            Assert.Equal(DnsTransportCapabilities.SupportsDnsOverQuic, DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverQuic));
            Assert.True(DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttps));
            Assert.False(DnsTransportCapabilities.Supports((DnsRequestFormat)int.MaxValue));
#if NET8_0_OR_GREATER
            Assert.True(DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverGrpc));
#else
            Assert.False(DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverGrpc));
#endif
        }

        /// <summary>Every public transport identifier reports its actual implementation and target support.</summary>
        [Fact]
        public void CapabilityReportIncludesJsonMulticastAndUnimplementedProtocols() {
            DnsTransportCapabilityInfo[] report = DnsTransportCapabilities.GetCapabilityReport();
            foreach (DnsRequestFormat format in Enum.GetValues(typeof(DnsRequestFormat))) {
                var entry = Assert.Single(report, item => item.RequestFormat == format);
                Assert.Equal(DnsTransportCapabilities.Supports(format), entry.Supported);
                Assert.False(string.IsNullOrWhiteSpace(entry.Notes));
            }
            Assert.Contains("custom", Assert.Single(report,
                item => item.RequestFormat == DnsRequestFormat.DnsOverGrpc).Name, StringComparison.OrdinalIgnoreCase);
            Assert.False(Assert.Single(report,
                item => item.RequestFormat == DnsRequestFormat.ObliviousDnsOverHttps).Supported);
            DnsTransportCapabilityInfo http3 = Assert.Single(report, entry => entry.RequestFormat == DnsRequestFormat.DnsOverHttp3);
            DnsTransportCapabilityInfo quic = Assert.Single(report, entry => entry.RequestFormat == DnsRequestFormat.DnsOverQuic);
            Assert.Equal(DnsTransportCapabilities.SupportsDnsOverHttp3, http3.Supported);
            Assert.Equal(DnsTransportCapabilities.SupportsDnsOverQuic, quic.Supported);
            Assert.Equal("DnsClientX", http3.Package);
            Assert.Equal("DnsClientX", quic.Package);
        }

        /// <summary>
        /// Ensures RFC 9103 capability reporting includes the macOS Network.framework boundary.
        /// </summary>
        [Fact]
        public void SupportsZoneTransferOverTls_ReportsCurrentRuntimeBoundary() {
#if NET8_0_OR_GREATER
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) {
                Assert.True(DnsTransportCapabilities.SupportsZoneTransferOverTls);
            } else if (OperatingSystem.IsMacOS()) {
#if NET10_0_OR_GREATER
                bool enabled = AppContext.TryGetSwitch(
                    "System.Net.Security.UseNetworkFramework", out bool configured) && configured;
                Assert.Equal(enabled, DnsTransportCapabilities.SupportsZoneTransferOverTls);
#else
                Assert.False(DnsTransportCapabilities.SupportsZoneTransferOverTls);
#endif
            } else {
                Assert.False(DnsTransportCapabilities.SupportsZoneTransferOverTls);
            }
#else
            Assert.False(DnsTransportCapabilities.SupportsZoneTransferOverTls);
#endif
        }

        private static bool GetRuntimeQuicSupport() {
#if NET8_0_OR_GREATER
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) {
                return false;
            }

            Type? quicConnectionType =
                Type.GetType("System.Net.Quic.QuicConnection, System.Net.Quic", throwOnError: false) ??
                Type.GetType("System.Net.Quic.QuicConnection", throwOnError: false);
            object? supported = quicConnectionType?
                .GetProperty("IsSupported", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null);
            return supported is bool value && value;
#else
            return false;
#endif
        }
    }
}
