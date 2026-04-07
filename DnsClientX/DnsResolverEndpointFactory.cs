using System;
using System.Collections.Generic;

namespace DnsClientX {
    /// <summary>
    /// Factory helpers to convert predefined <see cref="DnsEndpoint"/> values into <see cref="DnsResolverEndpoint"/> instances.
    /// Only transports supported by the multi-resolver (UDP, TCP, DoT, DoH) are emitted.
    /// </summary>
    public static class DnsResolverEndpointFactory {
        /// <summary>
        /// Creates one or more <see cref="DnsResolverEndpoint"/> entries for a predefined <see cref="DnsEndpoint"/>.
        /// DNSCrypt variants are not included because <see cref="Transport"/> does not currently model them.
        /// </summary>
        public static DnsResolverEndpoint[] From(DnsEndpoint endpoint) {
            var list = new List<DnsResolverEndpoint>();
            void AddHost(string host, int port, Transport transport, DnsRequestFormat? requestFormat = null) =>
                list.Add(new DnsResolverEndpoint { Host = host, Port = port, Transport = transport, RequestFormat = requestFormat });
            void AddDoh(string host, string path, DnsRequestFormat requestFormat) =>
                list.Add(new DnsResolverEndpoint {
                    Transport = Transport.Doh,
                    DohUrl = new Uri($"https://{host}{path}"),
                    Host = host,
                    Port = 443,
                    RequestFormat = requestFormat
                });

            switch (endpoint) {
                case DnsEndpoint.System:
                    foreach (var host in SystemInformation.GetDnsFromActiveNetworkCard()) AddHost(host, 53, Transport.Udp);
                    break;
                case DnsEndpoint.SystemTcp:
                    foreach (var host in SystemInformation.GetDnsFromActiveNetworkCard()) AddHost(host, 53, Transport.Tcp);
                    break;
                case DnsEndpoint.Cloudflare:
                    AddDoh("1.1.1.1", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    AddDoh("1.0.0.1", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.CloudflareWireFormat:
                    AddDoh("1.1.1.1", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    AddDoh("1.0.0.1", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.CloudflareWireFormatPost:
                    AddDoh("1.1.1.1", "/dns-query", DnsRequestFormat.DnsOverHttpsWirePost);
                    AddDoh("1.0.0.1", "/dns-query", DnsRequestFormat.DnsOverHttpsWirePost);
                    break;
                case DnsEndpoint.CloudflareJsonPost:
                    AddDoh("1.1.1.1", "/dns-query", DnsRequestFormat.DnsOverHttpsJSONPOST);
                    AddDoh("1.0.0.1", "/dns-query", DnsRequestFormat.DnsOverHttpsJSONPOST);
                    break;
                case DnsEndpoint.CloudflareSecurity:
                    AddDoh("1.1.1.2", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    AddDoh("1.0.0.2", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.CloudflareFamily:
                    AddDoh("1.1.1.3", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    AddDoh("1.0.0.3", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.Google:
                    AddDoh("8.8.8.8", "/resolve", DnsRequestFormat.DnsOverHttpsJSON);
                    AddDoh("8.8.4.4", "/resolve", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.GoogleWireFormat:
                    AddDoh("8.8.8.8", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    AddDoh("8.8.4.4", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.GoogleWireFormatPost:
                    AddDoh("8.8.8.8", "/dns-query", DnsRequestFormat.DnsOverHttpsWirePost);
                    AddDoh("8.8.4.4", "/dns-query", DnsRequestFormat.DnsOverHttpsWirePost);
                    break;
                case DnsEndpoint.GoogleJsonPost:
                    AddDoh("8.8.8.8", "/resolve", DnsRequestFormat.DnsOverHttpsJSONPOST);
                    AddDoh("8.8.4.4", "/resolve", DnsRequestFormat.DnsOverHttpsJSONPOST);
                    break;
                case DnsEndpoint.Quad9:
                    AddDoh("dns.quad9.net", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.Quad9ECS:
                    AddDoh("dns11.quad9.net", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.Quad9Unsecure:
                    AddDoh("dns10.quad9.net", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.OpenDNS:
                    AddDoh("208.67.222.222", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    AddDoh("208.67.220.220", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.OpenDNSFamily:
                    AddDoh("208.67.222.123", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    AddDoh("208.67.220.123", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.AdGuard:
                    AddDoh("dns.adguard.com", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.AdGuardFamily:
                    AddDoh("dns-family.adguard.com", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.AdGuardNonFiltering:
                    AddDoh("dns-unfiltered.adguard.com", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.NextDNS:
                    AddDoh("dns.nextdns.io", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.RootServer:
                    foreach (var host in RootServers.Servers) AddHost(host, 53, Transport.Udp);
                    break;
                case DnsEndpoint.CloudflareQuic:
                    AddHost("1.1.1.1", 853, Transport.Quic, DnsRequestFormat.DnsOverQuic);
                    AddHost("1.0.0.1", 853, Transport.Quic, DnsRequestFormat.DnsOverQuic);
                    break;
                case DnsEndpoint.GoogleQuic:
                    AddHost("8.8.8.8", 853, Transport.Quic, DnsRequestFormat.DnsOverQuic);
                    AddHost("8.8.4.4", 853, Transport.Quic, DnsRequestFormat.DnsOverQuic);
                    break;
                case DnsEndpoint.CloudflareOdoh:
                    AddDoh("odoh.cloudflare-dns.com", "/dns-query", DnsRequestFormat.ObliviousDnsOverHttps);
                    break;
                case DnsEndpoint.DnsCryptCloudflare:
                case DnsEndpoint.DnsCryptQuad9:
                case DnsEndpoint.DnsCryptRelay:
                case DnsEndpoint.Custom:
                default:
                    break;
            }

            return list.ToArray();
        }
    }
}
