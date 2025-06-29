using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Net;

namespace DnsClientX {
    /// <summary>
    /// Defines the class for getting system information.
    /// </summary>
    public class SystemInformation {
        /// <summary>
        /// Gets the DNS from active network card.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetDnsFromActiveNetworkCard() {
            var dnsServers = new List<string>();
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in networkInterfaces) {
                // Check if the network interface is active and has a default gateway
                if (networkInterface.OperationalStatus == OperationalStatus.Up) {
                    var properties = networkInterface.GetIPProperties();
                    if (properties.GatewayAddresses.Count > 0) {
                        foreach (var dnsAddress in properties.DnsAddresses) {
                            var address = dnsAddress.ToString();
                            if (dnsAddress.AddressFamily == AddressFamily.InterNetworkV6) {
                                address = address.Split('%')[0];
                                address = $"[{address}]";
                            }

                            if (!string.IsNullOrWhiteSpace(address)) {
                                dnsServers.Add(address);
                            }
                        }

                        // Once we find an active interface with a default gateway, we break the loop
                        break;
                    }
                }
            }

            if (dnsServers.Count == 0) {
                try {
                    foreach (var line in File.ReadAllLines("/etc/resolv.conf")) {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("nameserver", StringComparison.OrdinalIgnoreCase)) {
                            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1) {
                                var address = parts[1];
                                if (IPAddress.TryParse(address, out var ip)) {
                                    address = ip.ToString();
                                    if (ip.AddressFamily == AddressFamily.InterNetworkV6) {
                                        address = address.Split('%')[0];
                                        address = $"[{address}]";
                                    }
                                    dnsServers.Add(address);
                                }
                            }
                        }
                    }
                } catch {
                    // ignore if file not accessible
                }
            }

            return dnsServers;
        }
    }
}
