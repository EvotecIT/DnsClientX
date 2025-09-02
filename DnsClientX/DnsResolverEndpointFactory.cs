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
        /// Quic, gRPC, and DNSCrypt variants are not included as the multi-resolver currently supports UDP/TCP/DoT/DoH only.
        /// </summary>
        public static DnsResolverEndpoint[] From(DnsEndpoint endpoint) {
            var list = new List<DnsResolverEndpoint>();
            void AddHost(string host, int port, Transport transport) => list.Add(new DnsResolverEndpoint { Host = host, Port = port, Transport = transport });
            void AddDoh(string host) => list.Add(new DnsResolverEndpoint { Transport = Transport.Doh, DohUrl = new Uri($"https://{host}/dns-query"), Host = host, Port = 443 });

            switch (endpoint) {
                case DnsEndpoint.System:
                    foreach (var host in SystemInformation.GetDnsFromActiveNetworkCard()) AddHost(host, 53, Transport.Udp);
                    break;
                case DnsEndpoint.SystemTcp:
                    foreach (var host in SystemInformation.GetDnsFromActiveNetworkCard()) AddHost(host, 53, Transport.Tcp);
                    break;
                case DnsEndpoint.Cloudflare:
                case DnsEndpoint.CloudflareWireFormat:
                case DnsEndpoint.CloudflareWireFormatPost:
                case DnsEndpoint.CloudflareJsonPost:
                    AddDoh("1.1.1.1");
                    AddDoh("1.0.0.1");
                    break;
                case DnsEndpoint.CloudflareSecurity:
                    AddDoh("1.1.1.2");
                    AddDoh("1.0.0.2");
                    break;
                case DnsEndpoint.CloudflareFamily:
                    AddDoh("1.1.1.3");
                    AddDoh("1.0.0.3");
                    break;
                case DnsEndpoint.Google:
                case DnsEndpoint.GoogleWireFormat:
                case DnsEndpoint.GoogleWireFormatPost:
                case DnsEndpoint.GoogleJsonPost:
                    AddDoh("dns.google");
                    break;
                case DnsEndpoint.Quad9:
                case DnsEndpoint.Quad9ECS:
                case DnsEndpoint.Quad9Unsecure:
                    AddDoh("dns.quad9.net");
                    break;
                case DnsEndpoint.OpenDNS:
                case DnsEndpoint.OpenDNSFamily:
                    AddDoh("208.67.222.222");
                    AddDoh("208.67.220.220");
                    break;
                case DnsEndpoint.AdGuard:
                case DnsEndpoint.AdGuardFamily:
                case DnsEndpoint.AdGuardNonFiltering:
                    AddDoh("dns.adguard.com");
                    break;
                case DnsEndpoint.NextDNS:
                    AddDoh("dns.nextdns.io");
                    break;
                case DnsEndpoint.RootServer:
                    foreach (var host in RootServers.Servers) AddHost(host, 53, Transport.Udp);
                    break;
                // Not supported transports for multi-resolver (yet)
                case DnsEndpoint.CloudflareQuic:
                case DnsEndpoint.GoogleQuic:
                case DnsEndpoint.DnsCryptCloudflare:
                case DnsEndpoint.DnsCryptQuad9:
                case DnsEndpoint.DnsCryptRelay:
                case DnsEndpoint.CloudflareOdoh:
                case DnsEndpoint.Custom:
                default:
                    break;
            }

            return list.ToArray();
        }
    }
}
