using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Defines the class for getting system information.
    /// </summary>
    public class SystemInformation {
        /// <summary>
        /// Gets the DNS from active network card with improved reliability.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetDnsFromActiveNetworkCard() {
            var dnsServers = new List<string>();

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
                                // Filter out link-local and multicast addresses
                                if (!dnsAddress.IsIPv6LinkLocal && !dnsAddress.IsIPv6Multicast &&
                                    !dnsAddress.ToString().StartsWith("169.254.") && // IPv4 link-local
                                    !dnsAddress.ToString().StartsWith("fec0:")) { // IPv6 site-local
                                    dnsServers.Add(dnsAddress.ToString());
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
                                // Filter out problematic addresses
                                if (!dnsAddress.IsIPv6LinkLocal && !dnsAddress.IsIPv6Multicast &&
                                    !dnsAddress.ToString().StartsWith("169.254.") &&
                                    !dnsAddress.ToString().StartsWith("fec0:")) {
                                    dnsServers.Add(dnsAddress.ToString());
                                }
                            }

                            if (dnsServers.Count > 0) {
                                break;
                            }
                        }
                    }
                }
            } catch (Exception) {
                // If network interface enumeration fails, fall through to fallback
            }

            // Fallback: if no system DNS servers found, use well-known public DNS
            if (dnsServers.Count == 0) {
                // Use Cloudflare and Google as fallback - these are reliable and fast
                dnsServers.Add("1.1.1.1");    // Cloudflare Primary
                dnsServers.Add("8.8.8.8");    // Google Primary
            }

            return dnsServers;
        }
    }
}
