using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

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
                        var dnsAddresses = properties.DnsAddresses;
                        foreach (var dnsAddress in dnsAddresses) {
                            dnsServers.Add(dnsAddress.ToString());
                        }
                        // Once we find an active interface with a default gateway, we break the loop
                        break;
                    }
                }
            }

            return dnsServers;
        }
    }
}
