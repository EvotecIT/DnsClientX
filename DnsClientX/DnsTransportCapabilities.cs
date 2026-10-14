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
        private static readonly DnsRequestFormat[] DefaultReportFormats =
            Enum.GetValues(typeof(DnsRequestFormat)).Cast<DnsRequestFormat>().ToArray();

        /// <summary>
        /// Gets a value indicating whether DNS over HTTP/3 is supported by the current target/runtime combination.
        /// </summary>
        public static bool SupportsDnsOverHttp3 {
            get {
#if NET8_0_OR_GREATER
                return IsRuntimeQuicSupported();
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
                return IsRuntimeQuicSupported();
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

        /// <summary>Gets whether this target includes the custom DNS-over-gRPC service client.</summary>
        /// <remarks>This transport implements DnsResolver/QueryDns and requires a matching service.</remarks>
        public static bool SupportsDnsOverGrpc {
            get {
#if NET8_0_OR_GREATER
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current target and runtime can provide RFC 9103
        /// zone transfer over TLS with TLS 1.3 and the <c>dot</c> ALPN value.
        /// </summary>
        /// <remarks>
        /// On macOS, .NET 10 client-side TLS 1.3 requires the process-wide
        /// <c>System.Net.Security.UseNetworkFramework</c> AppContext switch to be enabled before
        /// the first TLS operation. DnsClientX reports that boundary but does not change a host
        /// application's process-wide TLS implementation.
        /// </remarks>
        public static bool SupportsZoneTransferOverTls {
            get {
#if NET8_0_OR_GREATER
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) return true;
                if (!OperatingSystem.IsMacOS()) return false;
#if NET10_0_OR_GREATER
                return AppContext.TryGetSwitch("System.Net.Security.UseNetworkFramework", out bool enabled)
                    && enabled;
#else
                return false;
#endif
#else
                return false;
#endif
            }
        }

        internal static string ZoneTransferOverTlsUnsupportedMessage {
            get {
#if NET8_0_OR_GREATER
                if (OperatingSystem.IsMacOS()) {
                    return "RFC 9103 XFR-over-TLS requires TLS 1.3 and the 'dot' ALPN value. "
                        + "On macOS this requires .NET 10 or newer and the "
                        + "System.Net.Security.UseNetworkFramework AppContext switch enabled before the first TLS operation.";
                }
#endif
                return "RFC 9103 XFR-over-TLS requires the net8.0 or newer target and a runtime with TLS 1.3 and 'dot' ALPN support.";
            }
        }

        /// <summary>
        /// Determines whether the given request format is supported by the current runtime.
        /// </summary>
        /// <param name="requestFormat">Request format to evaluate.</param>
        /// <returns><c>true</c> when the format is supported; otherwise, <c>false</c>.</returns>
        public static bool Supports(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverHttp3 => SupportsDnsOverHttp3,
                DnsRequestFormat.DnsOverQuic => SupportsDnsOverQuic,
                DnsRequestFormat.DnsOverGrpc => SupportsDnsOverGrpc,
                DnsRequestFormat.ObliviousDnsOverHttps => false,
                DnsRequestFormat.DnsCrypt => false,
                DnsRequestFormat.DnsCryptRelay => false,
                DnsRequestFormat.DnsOverUDP or DnsRequestFormat.DnsOverTCP or DnsRequestFormat.DnsOverTLS
                    or DnsRequestFormat.DnsOverHttps or DnsRequestFormat.DnsOverHttpsPOST
                    or DnsRequestFormat.DnsOverHttpsWirePost or DnsRequestFormat.DnsOverHttpsJSON
                    or DnsRequestFormat.DnsOverHttpsJSONPOST or DnsRequestFormat.DnsOverHttp2
                    or DnsRequestFormat.Multicast => true,
                _ => false
            };
        }

        /// <summary>
        /// Builds a user-facing capability report for the core transport surface.
        /// </summary>
        /// <param name="modernOnly">When set, returns only modern transports that have runtime-specific support gates.</param>
        /// <returns>Capability report entries.</returns>
        public static DnsTransportCapabilityInfo[] GetCapabilityReport(bool modernOnly = false) {
            IEnumerable<DnsRequestFormat> formats = modernOnly
                ? DefaultReportFormats.Where(format => format is DnsRequestFormat.DnsOverHttp3
                    or DnsRequestFormat.DnsOverQuic or DnsRequestFormat.DnsOverGrpc)
                : DefaultReportFormats;

            return formats
                .Select(CreateCapabilityInfo)
                .ToArray();
        }

        /// <summary>
        /// Builds unique runtime capability warnings for executed attempts that targeted unsupported transports.
        /// </summary>
        /// <param name="attempts">Executed attempts to inspect.</param>
        /// <returns>Unique warning strings ordered by target and request format.</returns>
        public static string[] GetUnsupportedWarnings(IEnumerable<ResolverQueryAttemptResult> attempts) {
            if (attempts == null) {
                throw new ArgumentNullException(nameof(attempts));
            }

            return attempts
                .Where(result => !Supports(result.RequestFormat))
                .GroupBy(result => $"{ResolverCandidateIdentity.GroupKey(result.CandidateId, result.Target)}|{result.RequestFormat}", StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(result => result.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.RequestFormat.ToString(), StringComparer.Ordinal)
                .Select(result => $"{result.Target}{(string.IsNullOrEmpty(result.CandidateId) ? string.Empty : " (" + result.CandidateId + ")")}: {GetUnsupportedMessage(result.RequestFormat)}")
                .ToArray();
        }

        /// <summary>
        /// Counts unique targets whose configured request format is unsupported on the current runtime.
        /// </summary>
        /// <param name="attempts">Executed attempts to inspect.</param>
        /// <returns>Unique unsupported target count.</returns>
        public static int CountUnsupportedTargets(IEnumerable<ResolverQueryAttemptResult> attempts) {
            if (attempts == null) {
                throw new ArgumentNullException(nameof(attempts));
            }

            return attempts
                .Where(result => !Supports(result.RequestFormat))
                .Select(result => ResolverCandidateIdentity.GroupKey(result.CandidateId, result.Target))
                .Distinct(StringComparer.Ordinal)
                .Count();
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
                DnsRequestFormat.DnsOverGrpc =>
                    "The custom DNS-over-gRPC service client requires the net8.0 or newer target and is unavailable in this target.",
                DnsRequestFormat.ObliviousDnsOverHttps =>
                    "ODoH requires an OdohClient from the optional DnsClientX.Odoh package in Configuration.WireTransport.",
                DnsRequestFormat.DnsCrypt or DnsRequestFormat.DnsCryptRelay =>
                    "DNSCrypt v2 requires a DnsCryptClient from the optional DnsClientX.DnsCrypt package and its native libsodium runtime.",
                _ => $"{requestFormat} is not supported on this runtime."
            };
        }

        /// <summary>Reports the configured transport, including an explicitly supplied optional provider.</summary>
        /// <remarks>Provider registration does not establish remote endpoint reachability.</remarks>
        public static DnsTransportCapabilityInfo GetConfiguredCapability(Configuration configuration) {
            if (configuration == null) {
                throw new ArgumentNullException(nameof(configuration));
            }
            DnsTransportCapabilityInfo result = CreateCapabilityInfo(configuration.RequestFormat);
            if (configuration.RequestFormat is DnsRequestFormat.ObliviousDnsOverHttps
                or DnsRequestFormat.DnsCrypt or DnsRequestFormat.DnsCryptRelay) {
                result.Package = configuration.RequestFormat == DnsRequestFormat.ObliviousDnsOverHttps
                    ? "DnsClientX.Odoh" : "DnsClientX.DnsCrypt";
                result.Supported = configuration.WireTransport != null
                    && configuration.WireTransport.RequestFormat == configuration.RequestFormat;
                result.TargetFrameworkScope = "netstandard2.0, .NET Framework 4.7.2, .NET 8+";
                result.RuntimeRequirement = configuration.RequestFormat == DnsRequestFormat.ObliviousDnsOverHttps
                    ? "Bouncy Castle and an HTTPS relay/target pair" : "Native libsodium for the host architecture";
                result.Notes = result.Supported ? "An optional provider is explicitly configured; its caller owns its lifetime."
                    : GetUnsupportedMessage(configuration.RequestFormat);
            }
            return result;
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
                DnsRequestFormat.DnsOverHttps or DnsRequestFormat.DnsOverHttpsPOST
                    or DnsRequestFormat.DnsOverHttpsWirePost => new DnsTransportCapabilityInfo {
                    Name = requestFormat == DnsRequestFormat.DnsOverHttps ? "DNS over HTTPS (wire GET)" : "DNS over HTTPS (wire POST)",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "RFC 8484 wire-format DNS over HTTPS."
                },
                DnsRequestFormat.DnsOverHttpsJSON or DnsRequestFormat.DnsOverHttpsJSONPOST => new DnsTransportCapabilityInfo {
                    Name = requestFormat == DnsRequestFormat.DnsOverHttpsJSON ? "DNS over HTTPS (JSON GET)" : "DNS over HTTPS (JSON POST)",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    Notes = "Provider JSON API; requires an endpoint implementing the selected JSON contract."
                },
                DnsRequestFormat.Multicast => new DnsTransportCapabilityInfo {
                    Name = "Multicast DNS",
                    RequestFormat = requestFormat,
                    Supported = true,
                    TargetFrameworkScope = "All supported targets",
                    RuntimeRequirement = "Requires access to the selected local multicast interface",
                    Notes = "RFC 6762 local-link DNS; availability depends on network and interface policy."
                },
                DnsRequestFormat.DnsOverGrpc => new DnsTransportCapabilityInfo {
                    Name = "DNS over gRPC (custom service)",
                    RequestFormat = requestFormat,
                    Supported = SupportsDnsOverGrpc,
                    TargetFrameworkScope = ".NET 8+",
                    RuntimeRequirement = "Requires a compatible DnsResolver/QueryDns service over HTTP/2",
                    Notes = SupportsDnsOverGrpc
                        ? "Custom service contract, not a standardized DNS transport."
                        : GetUnsupportedMessage(requestFormat)
                },
                DnsRequestFormat.DnsCrypt or DnsRequestFormat.DnsCryptRelay
                    or DnsRequestFormat.ObliviousDnsOverHttps => new DnsTransportCapabilityInfo {
                    Name = requestFormat == DnsRequestFormat.ObliviousDnsOverHttps ? "Oblivious DNS over HTTPS" : requestFormat.ToString(),
                    RequestFormat = requestFormat,
                    Supported = false,
                    TargetFrameworkScope = "Not implemented",
                    Notes = GetUnsupportedMessage(requestFormat)
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

#if NET8_0_OR_GREATER
        private static bool IsRuntimeQuicSupported() {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS()) {
                return false;
            }

            Type? quicConnectionType =
                Type.GetType("System.Net.Quic.QuicConnection, System.Net.Quic", throwOnError: false) ??
                Type.GetType("System.Net.Quic.QuicConnection", throwOnError: false);
            object? supported = quicConnectionType?
                .GetProperty("IsSupported", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?
                .GetValue(null);

            return supported is bool value && value;
        }
#endif
    }
}
