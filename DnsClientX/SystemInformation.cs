using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Net;

namespace DnsClientX {
    /// <summary>
    /// Defines the class for getting system information.
    /// </summary>
    public class SystemInformation {
        /// <summary>
        /// Gets the DNS from active network card with improved cross-platform reliability.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetDnsFromActiveNetworkCard() {
            var dnsServers = new List<string>();
            bool debug = Environment.GetEnvironmentVariable("DNSCLIENTX_DEBUG_SYSTEMDNS") == "1";

            void DebugPrint(string msg) { if (debug) Console.WriteLine($"[DnsClientX:SystemDNS] {msg}"); }

            try {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                // First, try to find interfaces with both gateway and DNS servers
                foreach (var networkInterface in networkInterfaces) {
                    if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                        networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback) {

                        var properties = networkInterface.GetIPProperties();

                        // Prefer interfaces with default gateways (internet-connected)
                        if (properties.GatewayAddresses.Count > 0) {
                            var dnsAddresses = properties.DnsAddresses;
                            foreach (var dnsAddress in dnsAddresses) {
                                var formattedAddress = FormatDnsAddress(dnsAddress);
                                if (!string.IsNullOrWhiteSpace(formattedAddress)) {
                                    if (IsValidDnsAddress(dnsAddress)) {
                                        DebugPrint($"[Interface-Gateway] Found DNS: {dnsAddress}");
                                        dnsServers.Add(formattedAddress);
                                    } else {
                                        DebugPrint($"[Interface-Gateway] Filtered out DNS: {dnsAddress}");
                                    }
                                }
                            }

                            // If we found DNS servers from an interface with gateway, use them
                            if (dnsServers.Count > 0) {
                                break;
                            }
                        }
                    }
                }

                // If no DNS servers found from interfaces with gateways,
                // try any active interface with DNS servers
                if (dnsServers.Count == 0) {
                    foreach (var networkInterface in networkInterfaces) {
                        if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                            networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback) {

                            var properties = networkInterface.GetIPProperties();
                            var dnsAddresses = properties.DnsAddresses;

                            foreach (var dnsAddress in dnsAddresses) {
                                var formattedAddress = FormatDnsAddress(dnsAddress);
                                if (!string.IsNullOrWhiteSpace(formattedAddress)) {
                                    if (IsValidDnsAddress(dnsAddress)) {
                                        DebugPrint($"[Interface-Any] Found DNS: {dnsAddress}");
                                        dnsServers.Add(formattedAddress);
                                    } else {
                                        DebugPrint($"[Interface-Any] Filtered out DNS: {dnsAddress}");
                                    }
                                }
                            }

                            if (dnsServers.Count > 0) {
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
                try {
                    if (File.Exists("/etc/resolv.conf")) {
                        foreach (var line in File.ReadAllLines("/etc/resolv.conf")) {
                            var trimmed = line.Trim();
                            if (trimmed.StartsWith("nameserver", StringComparison.OrdinalIgnoreCase)) {
                                var parts = trimmed.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length > 1) {
                                    var address = parts[1];
                                    DebugPrint($"[resolv.conf] Found nameserver: {address}");
                                    if (IPAddress.TryParse(address, out var ip)) {
                                        var formattedAddress = FormatDnsAddress(ip);
                                        if (!string.IsNullOrWhiteSpace(formattedAddress)) {
                                            if (IsValidDnsAddress(ip)) {
                                                DebugPrint($"[resolv.conf] Accepted: {address}");
                                                dnsServers.Add(formattedAddress);
                                            } else {
                                                DebugPrint($"[resolv.conf] Filtered out: {address}");
                                            }
                                        }
                                    } else {
                                        DebugPrint($"[resolv.conf] Not a valid IP: {address}");
                                    }
                                }
                            }
                        }
                    } else {
                        DebugPrint("/etc/resolv.conf does not exist");
                    }
                } catch (Exception ex) {
                    DebugPrint($"Exception reading /etc/resolv.conf: {ex.Message}");
                    // Ignore if file not accessible or other I/O errors
                }
            }

            // Final fallback: if no system DNS servers found, use well-known public DNS
            if (dnsServers.Count == 0) {
                DebugPrint("No system DNS found, using fallback public DNS: 1.1.1.1, 8.8.8.8");
                dnsServers.Add("1.1.1.1");    // Cloudflare Primary
                dnsServers.Add("8.8.8.8");    // Google Primary
            }

            DebugPrint($"Returning DNS servers: {string.Join(", ", dnsServers)}");
            return dnsServers;
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
                addressString = addressString.Split('%')[0];
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
            if (address == null) return false;

            // Filter out problematic addresses
            if (address.IsIPv6LinkLocal || address.IsIPv6Multicast) {
                return false;
            }

            var addressString = address.ToString();

            // Filter out IPv4 link-local addresses (169.254.x.x)
            if (addressString.StartsWith("169.254.")) {
                return false;
            }

            // Filter out IPv6 site-local addresses (deprecated)
            if (addressString.StartsWith("fec0:")) {
                return false;
            }

            return true;
        }
    }
}
