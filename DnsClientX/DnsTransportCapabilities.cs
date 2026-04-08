using System;

namespace DnsClientX {
    /// <summary>
    /// Reports transport capabilities available in the current runtime.
    /// </summary>
    public static class DnsTransportCapabilities {
        /// <summary>
        /// Gets a value indicating whether DNS over HTTP/3 is supported by the current target/runtime combination.
        /// </summary>
        public static bool SupportsDnsOverHttp3 {
            get {
#if NET8_0_OR_GREATER
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether DNS over QUIC is supported by the current target/runtime combination.
        /// </summary>
        public static bool SupportsDnsOverQuic {
            get {
#if NET8_0_OR_GREATER
                return OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether native QUIC APIs are available to the current runtime.
        /// </summary>
        public static bool SupportsQuic => SupportsDnsOverQuic;

        /// <summary>
        /// Gets a value indicating whether modern HTTP transports are available to the current runtime.
        /// </summary>
        public static bool SupportsModernHttp => SupportsDnsOverHttp3;

        /// <summary>
        /// Determines whether the given request format is supported by the current runtime.
        /// </summary>
        /// <param name="requestFormat">Request format to evaluate.</param>
        /// <returns><c>true</c> when the format is supported; otherwise, <c>false</c>.</returns>
        public static bool Supports(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverHttp3 => SupportsDnsOverHttp3,
                DnsRequestFormat.DnsOverQuic => SupportsDnsOverQuic,
                _ => true
            };
        }
    }
}
