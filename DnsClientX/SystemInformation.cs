using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace DnsClientX {
    /// <summary>
    /// Defines the class for getting system information.
    /// </summary>
    /// <remarks>
    /// Methods in this class assist in obtaining DNS server details from the operating system.
    /// </remarks>
    public class SystemInformation {
        private static Lazy<List<string>> cachedDnsServers = new(LoadDnsServers, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly object dnsServersLock = new();
        private static Func<List<string>>? dnsServerProvider;

        internal static void SetDnsServerProvider(Func<List<string>>? provider) {
            dnsServerProvider = provider;
            cachedDnsServers = new Lazy<List<string>>(LoadDnsServers, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Gets the DNS from active network card with improved cross-platform reliability.
        /// The results are cached for subsequent calls unless <paramref name="refresh"/> is <c>true</c>.
        /// </summary>
        /// <param name="refresh">Set to <c>true</c> to force cache refresh.</param>
        /// <returns></returns>
        public static List<string> GetDnsFromActiveNetworkCard(bool refresh = false) {
            if (refresh || dnsServerProvider != null && !cachedDnsServers.IsValueCreated) {
                var newLazy = new Lazy<List<string>>(LoadDnsServers, LazyThreadSafetyMode.ExecutionAndPublication);
                lock (dnsServersLock) {
                    if (refresh || dnsServerProvider != null && !cachedDnsServers.IsValueCreated) {
                        cachedDnsServers = newLazy;
                    }
                }
            }

            return new List<string>(cachedDnsServers.Value);
        }

        private static List<string> LoadDnsServers() {
            if (dnsServerProvider is not null) {
                try {
                    return DeduplicateDnsServers(dnsServerProvider.Invoke() ?? new List<string>());
                } catch {
                    return new List<string>();
                }
            }

            var dnsServers = new List<string>();
            bool debug = Environment.GetEnvironmentVariable("DNSCLIENTX_DEBUG_SYSTEMDNS") == "1";

            void DebugPrint(string msg) { if (debug) Settings.Logger.WriteDebug($"[DnsClientX:SystemDNS] {msg}"); }

            try {
                DebugPrint("Starting DNS server discovery...");
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                DebugPrint($"Found {networkInterfaces.Length} network interfaces");

                // First, try to find interfaces with both gateway and DNS servers
                foreach (var networkInterface in networkInterfaces) {
                    if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                        networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback) {

                        DebugPrint($"Checking interface: {networkInterface.Name} ({networkInterface.NetworkInterfaceType})");
                        var properties = networkInterface.GetIPProperties();

                        // Prefer interfaces with default gateways (internet-connected)
                        if (properties.GatewayAddresses.Count > 0) {
                            DebugPrint($"Interface {networkInterface.Name} has {properties.GatewayAddresses.Count} gateways");
                            var dnsAddresses = properties.DnsAddresses;
                            foreach (var dnsAddress in dnsAddresses) {
                                var formattedAddress = FormatDnsAddress(dnsAddress);
                                if (!string.IsNullOrWhiteSpace(formattedAddress)) {
                                    if (IsValidDnsAddress(dnsAddress)) {
                                        DebugPrint($"[Interface-Gateway] Found DNS: {dnsAddress} on interface {networkInterface.Name}");
                                        dnsServers.Add(formattedAddress);
                                    } else {
                                        DebugPrint($"[Interface-Gateway] Filtered out DNS: {dnsAddress} on interface {networkInterface.Name}");
                                    }
                                }
                            }

                            // If we found DNS servers from an interface with gateway, use them
                            if (dnsServers.Count > 0) {
                                DebugPrint($"Using DNS servers from interface {networkInterface.Name} with gateway");
                                break;
                            }
                        } else {
                            DebugPrint($"Interface {networkInterface.Name} has no gateways");
                        }
                    } else {
                        DebugPrint($"Skipping interface {networkInterface.Name} (Status: {networkInterface.OperationalStatus}, Type: {networkInterface.NetworkInterfaceType})");
                    }
                }

                // If no DNS servers found from interfaces with gateways,
                // try any active interface with DNS servers
                if (dnsServers.Count == 0) {
                    DebugPrint("No DNS servers found from interfaces with gateways, checking all active interfaces");
                    foreach (var networkInterface in networkInterfaces) {
                        if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                            networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback) {

                            DebugPrint($"Checking interface without gateway: {networkInterface.Name}");
                            var properties = networkInterface.GetIPProperties();
                            var dnsAddresses = properties.DnsAddresses;

                            foreach (var dnsAddress in dnsAddresses) {
                                var formattedAddress = FormatDnsAddress(dnsAddress);
                                if (!string.IsNullOrWhiteSpace(formattedAddress)) {
                                    if (IsValidDnsAddress(dnsAddress)) {
                                        DebugPrint($"[Interface-Any] Found DNS: {dnsAddress} on interface {networkInterface.Name}");
                                        dnsServers.Add(formattedAddress);
                                    } else {
                                        DebugPrint($"[Interface-Any] Filtered out DNS: {dnsAddress} on interface {networkInterface.Name}");
                                    }
                                }
                            }

                            if (dnsServers.Count > 0) {
                                DebugPrint($"Using DNS servers from interface {networkInterface.Name}");
                                break;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                DebugPrint($"Exception in network interface enumeration: {ex.Message}");
                // If network interface enumeration fails, fall through to Unix/Linux fallback
            }

            // Unix/Linux fallback: try reading /etc/resolv.conf
            if (dnsServers.Count == 0) {
                DebugPrint("No DNS servers found from network interfaces, trying /etc/resolv.conf");
                dnsServers.AddRange(ParseResolvConf("/etc/resolv.conf", DebugPrint));
            }

            // Final fallback: if no system DNS servers found, use well-known public DNS
            if (dnsServers.Count == 0) {
                DebugPrint("No system DNS found, using fallback public DNS: 1.1.1.1, 8.8.8.8");
                dnsServers.Add(FormatDnsAddress(IPAddress.Parse("1.1.1.1"))); // Cloudflare Primary
                dnsServers.Add(FormatDnsAddress(IPAddress.Parse("8.8.8.8"))); // Google Primary
            }

            dnsServers = dnsServers.Distinct().ToList();
            DebugPrint($"Final DNS server list: {string.Join(", ", dnsServers)}");

            return dnsServers;
        }

        internal static List<string> ParseResolvConf(string path, Action<string>? debugPrint = null) {
            var servers = new List<string>();

            if (!File.Exists(path)) {
                debugPrint?.Invoke($"Skipping {path}; file not found");
                return servers;
            }

            debugPrint?.Invoke($"Reading {path}");

            try {
                foreach (var line in File.ReadAllLines(path)) {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) {
                        continue;
                    }
                    if (trimmed.StartsWith("nameserver", StringComparison.OrdinalIgnoreCase)) {
                        var parts = trimmed.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1) {
                            var address = parts[1];
                            debugPrint?.Invoke($"[resolv.conf] Found nameserver: {address}");
                            if (IPAddress.TryParse(address, out var ip)) {
                                var formattedAddress = FormatDnsAddress(ip);
                                if (!string.IsNullOrWhiteSpace(formattedAddress)) {
                                    if (IsValidDnsAddress(ip)) {
                                        debugPrint?.Invoke($"[resolv.conf] Accepted: {address}");
                                        servers.Add(formattedAddress);
                                    } else {
                                        debugPrint?.Invoke($"[resolv.conf] Filtered out: {address}");
                                    }
                                }
                            } else {
                                debugPrint?.Invoke($"[resolv.conf] Not a valid IP: {address}");
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                debugPrint?.Invoke($"Exception reading {path}: {ex.Message}");
                // Ignore if file not accessible or other I/O errors
            }

            return servers;
        }

        private static List<string> DeduplicateDnsServers(IEnumerable<string> servers) {
            var unique = new OrderedDictionary(StringComparer.Ordinal);

            foreach (var server in servers) {
                if (!unique.Contains(server)) {
                    unique.Add(server, null);
                }
            }

            return unique.Keys.Cast<string>().ToList();
        }

        /// <summary>
        /// Formats a DNS address properly, handling IPv6 zone identifiers and brackets.
        /// </summary>
        /// <param name="address">The IP address to format</param>
        /// <returns>Properly formatted DNS address string</returns>
        private static string FormatDnsAddress(IPAddress address) {
            if (address == null) return string.Empty;

            var addressString = address.ToString();

            if (address.AddressFamily == AddressFamily.InterNetworkV6) {
                // Remove zone identifier (e.g., %15) for IPv6 addresses
                int zoneIndex = addressString.IndexOf('%');
                if (zoneIndex > -1) {
                    addressString = addressString.Substring(0, zoneIndex);
                }

                // Normalize loopback address so it is always ::1
                if (IPAddress.IPv6Loopback.Equals(address)) {
                    addressString = "::1";
                }

                // Wrap IPv6 addresses in brackets for proper DNS formatting
                addressString = $"[{addressString}]";
            }

            return addressString;
        }

        /// <summary>
        /// Validates if an IP address is suitable for use as a DNS server.
        /// </summary>
        /// <param name="address">The IP address to validate</param>
        /// <returns>True if the address is valid for DNS use</returns>
        private static bool IsValidDnsAddress(IPAddress address) {
            if (address is null) {
                return false;
            }

            // Convert to string for pattern matching
            string ipString = address.ToString();

            // Filter out known problematic addresses
            if (address.AddressFamily == AddressFamily.InterNetwork) {
                // IPv4 filtering

                // Filter out link-local addresses (169.254.x.x)
                if (ipString.StartsWith("169.254.")) return false;

                // Filter out loopback addresses (127.x.x.x)
                if (ipString.StartsWith("127.")) return false;

                // Filter out macOS virtual network interface addresses (192.168.64.x)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && ipString.StartsWith("192.168.64.")) {
                    bool debug = Environment.GetEnvironmentVariable("DNSCLIENTX_DEBUG_SYSTEMDNS") == "1";
                    if (debug) Settings.Logger.WriteDebug($"[DnsClientX:SystemDNS] Filtering out macOS virtual network DNS: {ipString}");
                    return false;
                }

                return true;
            } else if (address.AddressFamily == AddressFamily.InterNetworkV6) {
                // IPv6 filtering

                // Filter out link-local addresses (fe80:)
                if (ipString.StartsWith("fe80:")) return false;

                // Filter out site-local addresses (fec0: - deprecated)
                if (ipString.StartsWith("fec0:")) return false;

                // Filter out multicast addresses (ff00:)
                if (ipString.StartsWith("ff00:")) return false;

                // Filter out other multicast addresses starting with ff
                if (ipString.StartsWith("ff")) return false;

                return true;
            }

            return false;
        }
    }
}
