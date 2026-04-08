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
            Assert.True(DnsTransportCapabilities.SupportsDnsOverHttp3);
            Assert.Equal(
                OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
                DnsTransportCapabilities.SupportsDnsOverQuic);
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
    }
}
