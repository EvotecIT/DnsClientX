using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Factory helpers to convert predefined <see cref="DnsEndpoint"/> values into <see cref="DnsResolverEndpoint"/> instances.
    /// Built-in entries cover the core transport families supported by the shared resolver workflows.
    /// </summary>
    public static class DnsResolverEndpointFactory {
        /// <summary>
        /// Expands predefined providers into their concrete resolver endpoints and removes duplicates.
        /// </summary>
        public static DnsResolverEndpoint[] From(IEnumerable<DnsEndpoint> endpoints) {
            if (endpoints == null) throw new ArgumentNullException(nameof(endpoints));
            var expanded = new List<DnsResolverEndpoint>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DnsEndpoint endpoint in endpoints) {
                foreach (DnsResolverEndpoint candidate in From(endpoint)) {
                    string key = $"{candidate.Transport}|{candidate.RequestFormat}|{candidate.DohUrl}|{candidate.Host}|{candidate.Port}|{candidate.Family}|{candidate.TlsServerName}";
                    if (seen.Add(key)) expanded.Add(candidate);
                }
            }
            return expanded.ToArray();
        }

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
                    throw new NotSupportedException("Cloudflare does not publish a JSON-over-POST DNS endpoint. Use CloudflareWireFormatPost for RFC 8484 POST.");
                case DnsEndpoint.CloudflareSecurity:
                    AddDoh("1.1.1.2", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    AddDoh("1.0.0.2", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.CloudflareFamily:
                    AddDoh("1.1.1.3", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    AddDoh("1.0.0.3", "/dns-query", DnsRequestFormat.DnsOverHttpsJSON);
                    break;
                case DnsEndpoint.Google:
                    AddDoh("dns.google", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.GoogleWireFormat:
                    AddDoh("dns.google", "/dns-query", DnsRequestFormat.DnsOverHttps);
                    break;
                case DnsEndpoint.GoogleWireFormatPost:
                    AddDoh("dns.google", "/dns-query", DnsRequestFormat.DnsOverHttpsWirePost);
                    break;
                case DnsEndpoint.GoogleJsonPost:
                    throw new NotSupportedException("Google Public DNS does not publish a JSON-over-POST endpoint. Use GoogleWireFormatPost for RFC 8484 POST.");
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
                    throw new NotSupportedException("Cloudflare does not publish a DNS-over-QUIC resolver endpoint.");
                case DnsEndpoint.Quad9Http3:
                    AddDoh("dns.quad9.net", "/dns-query", DnsRequestFormat.DnsOverHttp3);
                    break;
                case DnsEndpoint.Quad9Quic:
                    AddHost("dns.quad9.net", 853, Transport.Quic, DnsRequestFormat.DnsOverQuic);
                    break;
                case DnsEndpoint.GoogleQuic:
                    throw new NotSupportedException("Google Public DNS does not publish a DNS-over-QUIC resolver endpoint.");
                case DnsEndpoint.CloudflareOdoh:
                    throw new NotSupportedException("ODoH is intentionally not implemented by the core package.");
                case DnsEndpoint.DnsCryptCloudflare:
                case DnsEndpoint.DnsCryptQuad9:
                case DnsEndpoint.DnsCryptRelay:
                    throw new NotSupportedException("DNSCrypt v2 is reserved for an optional protocol package.");
                case DnsEndpoint.Custom:
                default:
                    break;
            }

            return list.ToArray();
        }
    }
}
