using System.Reflection;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared runtime transport capability reporting.
    /// </summary>
    public class DnsTransportCapabilitiesTests {
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
        }

        /// <summary>
        /// Ensures the user-facing capability report exposes modern transport entries.
        /// </summary>
        [Fact]
        public void GetCapabilityReport_ContainsModernEntries() {
            DnsTransportCapabilityInfo[] report = DnsTransportCapabilities.GetCapabilityReport();

            DnsTransportCapabilityInfo http3 = Assert.Single(report, entry => entry.RequestFormat == DnsRequestFormat.DnsOverHttp3);
            DnsTransportCapabilityInfo quic = Assert.Single(report, entry => entry.RequestFormat == DnsRequestFormat.DnsOverQuic);

            Assert.Equal(DnsTransportCapabilities.SupportsDnsOverHttp3, http3.Supported);
            Assert.Equal(DnsTransportCapabilities.SupportsDnsOverQuic, quic.Supported);
            Assert.Equal("DnsClientX", http3.Package);
            Assert.Equal("DnsClientX", quic.Package);
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
