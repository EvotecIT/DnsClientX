using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX {
    /// <summary>
    /// Parses user-provided resolver endpoint strings into validated endpoints.
    /// </summary>
    public static class EndpointParser {
        /// <summary>
        /// Tries to parse multiple endpoint input strings.
        /// Accepted formats:
        ///  - IPv4: "1.1.1.1:53"
        ///  - IPv6: "[2606:4700:4700::1111]:53"
        ///  - Hostname: "dns.google:53"
        ///  - DoH URL: "https://dns.google/dns-query"
        /// </summary>
        public static DnsResolverEndpoint[] TryParseMany(IEnumerable<string> inputs, out IReadOnlyList<string> errors) {
            var list = new List<DnsResolverEndpoint>();
            var errs = new List<string>();

            foreach (var raw in inputs ?? Array.Empty<string>()) {
                if (string.IsNullOrWhiteSpace(raw)) {
                    errs.Add("Empty endpoint string");
                    continue;
                }

                // DoH URL
                if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                    if (Uri.TryCreate(raw, UriKind.Absolute, out var uri)) {
                        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) {
                            errs.Add($"Unsupported scheme for DoH: {raw}");
                            continue;
                        }
                        list.Add(new DnsResolverEndpoint {
                            Transport = Transport.Doh,
                            DohUrl = uri,
                            Host = uri.Host,
                            Port = uri.IsDefaultPort ? 443 : uri.Port
                        });
                        continue;
                    }
                    errs.Add($"Invalid DoH URL: {raw}");
                    continue;
                }

                // IPv6 in brackets
                if (raw.StartsWith("[")) {
                    int end = raw.IndexOf(']');
                    if (end > 1) {
                        string ip = raw.Substring(1, end - 1);
                        string portPart = raw.Length > end + 1 && raw[end + 1] == ':' ? raw.Substring(end + 2) : "53";
                        if (IPAddress.TryParse(ip, out var addr) && addr.AddressFamily == AddressFamily.InterNetworkV6 && int.TryParse(portPart, out int p) && p > 0 && p <= 65535) {
                            list.Add(new DnsResolverEndpoint { Host = ip, Port = p, Transport = Transport.Udp, Family = AddressFamily.InterNetworkV6 });
                            continue;
                        }
                    }
                    errs.Add($"Invalid IPv6 endpoint: {raw}");
                    continue;
                }

                // Split host:port (IPv4 or hostname)
                var parts = raw.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out int port) && port > 0 && port <= 65535) {
                    string host = parts[0];
                    AddressFamily? family = null;
                    if (IPAddress.TryParse(host, out var ipAddr)) {
                        family = ipAddr.AddressFamily;
                    }
                    list.Add(new DnsResolverEndpoint { Host = host, Port = port, Transport = Transport.Udp, Family = family });
                    continue;
                }

                // Plain host with default port
                if (!string.IsNullOrWhiteSpace(raw)) {
                    AddressFamily? family = null;
                    if (IPAddress.TryParse(raw, out var ipAddr)) {
                        family = ipAddr.AddressFamily;
                    }
                    list.Add(new DnsResolverEndpoint { Host = raw, Port = 53, Transport = Transport.Udp, Family = family });
                    continue;
                }

                errs.Add($"Unrecognized endpoint format: {raw}");
            }

            errors = errs;
            return list.ToArray();
        }
    }
}
