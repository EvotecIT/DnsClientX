using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Reports transport capabilities available in the current runtime.
    /// </summary>
    /// <remarks>
    /// Modern transports such as DNS over HTTP/3 and DNS over QUIC remain part of the core package
    /// and do not add extra NuGet dependencies for modern target frameworks. Older targets can still
    /// reference the same API surface, but unsupported transports are reported explicitly at runtime.
    /// </remarks>
    public static class DnsTransportCapabilities {
        private static readonly DnsRequestFormat[] DefaultReportFormats = new[] {
            DnsRequestFormat.DnsOverUDP,
            DnsRequestFormat.DnsOverTCP,
            DnsRequestFormat.DnsOverTLS,
            DnsRequestFormat.DnsOverHttps,
            DnsRequestFormat.DnsOverHttp2,
            DnsRequestFormat.DnsOverHttp3,
            DnsRequestFormat.DnsOverQuic
        };

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
        /// <remarks>
        /// Support depends on both the target framework and the active runtime QUIC implementation.
        /// </remarks>
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

        /// <summary>
        /// Builds a user-facing capability report for the core transport surface.
        /// </summary>
        /// <param name="modernOnly">When set, returns only modern transports that have runtime-specific support gates.</param>
        /// <returns>Capability report entries.</returns>
        public static DnsTransportCapabilityInfo[] GetCapabilityReport(bool modernOnly = false) {
            IEnumerable<DnsRequestFormat> formats = modernOnly
                ? DefaultReportFormats.Where(format => format == DnsRequestFormat.DnsOverHttp3 || format == DnsRequestFormat.DnsOverQuic)
                : DefaultReportFormats;

            return formats
                .Select(CreateCapabilityInfo)
                .ToArray();
        }

        /// <summary>
        /// Returns a human-readable unsupported-transport message for the given request format.
        /// </summary>
        /// <param name="requestFormat">Request format to describe.</param>
        /// <returns>Unsupported message when the format is not available on the current runtime.</returns>
        public static string GetUnsupportedMessage(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverHttp3 =>
                    "DNS over HTTP/3 is supported in the core package on net8+ and is unavailable on this runtime.",
                DnsRequestFormat.DnsOverQuic =>
                    "DNS over QUIC is supported in the core package on net8+ when the runtime provides QUIC support and is unavailable on this runtime.",
                _ => $"{requestFormat} is not supported on this runtime."
            };
        }

        private static DnsTransportCapabilityInfo CreateCapabilityInfo(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverUDP => new DnsTransportCapabilityInfo {
                    Name = "DNS over UDP",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "Core transport with optional TCP fallback when truncation is detected."
                },
                DnsRequestFormat.DnsOverTCP => new DnsTransportCapabilityInfo {
                    Name = "DNS over TCP",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "Core transport for direct TCP resolver queries and larger payloads."
                },
                DnsRequestFormat.DnsOverTLS => new DnsTransportCapabilityInfo {
                    Name = "DNS over TLS",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "Encrypted core transport available without adding extra transport packages."
                },
                DnsRequestFormat.DnsOverHttps => new DnsTransportCapabilityInfo {
                    Name = "DNS over HTTPS",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "Covers the shared DoH family used by browser-safe and local workflows."
                },
                DnsRequestFormat.DnsOverHttp2 => new DnsTransportCapabilityInfo {
                    Name = "DNS over HTTP/2",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "Core HTTP transport path for wire-format DoH requests."
                },
                DnsRequestFormat.DnsOverHttp3 => new DnsTransportCapabilityInfo {
                    Name = "DNS over HTTP/3",
                    RequestFormat = requestFormat,
                    Supported = SupportsDnsOverHttp3,
                    TargetFrameworkScope = ".NET 8+",
                    RuntimeRequirement = "Requires runtime HTTP/3 support",
                    Notes = SupportsDnsOverHttp3
                        ? "Available in the core package with no extra NuGet transport dependencies."
                        : GetUnsupportedMessage(requestFormat)
                },
                DnsRequestFormat.DnsOverQuic => new DnsTransportCapabilityInfo {
                    Name = "DNS over QUIC",
                    RequestFormat = requestFormat,
                    Supported = SupportsDnsOverQuic,
                    TargetFrameworkScope = ".NET 8+",
                    RuntimeRequirement = "Requires runtime QUIC support",
                    Notes = SupportsDnsOverQuic
                        ? "Available in the core package with no extra NuGet transport dependencies."
                        : GetUnsupportedMessage(requestFormat)
                },
                _ => new DnsTransportCapabilityInfo {
                    Name = requestFormat.ToString(),
                    RequestFormat = requestFormat,
                    Supported = Supports(requestFormat),
                    TargetFrameworkScope = "Runtime-dependent",
                    RuntimeRequirement = "See request format notes",
                    Notes = Supports(requestFormat) ? "Supported by the current runtime." : GetUnsupportedMessage(requestFormat)
                }
            };
        }
    }
}
