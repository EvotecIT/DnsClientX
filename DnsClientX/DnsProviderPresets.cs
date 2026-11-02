using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX;

/// <summary>Published provider identities shared by configuration, endpoint expansion and documentation.</summary>
internal static class DnsProviderPresets {
    internal sealed class Profile {
        internal string[] Hosts { get; }
        internal string? DohPath { get; }
        internal string? JsonPath { get; }
        internal DnsRequestFormat DefaultFormat { get; }
        internal string[] Addresses { get; }
        internal string? TlsName { get; }
        internal bool Quic { get; }
        internal bool Http3 { get; }
        internal Profile(string[] hosts, string? path, DnsRequestFormat format, string[] addresses,
            string? tls = null, bool quic = false, bool http3 = false, string? jsonPath = null) {
            Hosts = hosts; DohPath = path; DefaultFormat = format; Addresses = addresses;
            JsonPath = jsonPath ?? (format == DnsRequestFormat.DnsOverHttpsJSON ? path : null);
            TlsName = tls; Quic = quic; Http3 = http3;
        }
        internal DnsRequestFormat[] Formats => new[] {
            DnsRequestFormat.DnsOverUDP, DnsRequestFormat.DnsOverTCP, DnsRequestFormat.DnsOverTLS,
            DnsRequestFormat.DnsOverHttps, DnsRequestFormat.DnsOverHttpsWirePost,
            DnsRequestFormat.DnsOverHttpsJSON, DnsRequestFormat.DnsOverHttp3, DnsRequestFormat.DnsOverQuic
        }.Where(Supports).ToArray();
        private bool Supports(DnsRequestFormat format) => format switch {
            DnsRequestFormat.DnsOverUDP or DnsRequestFormat.DnsOverTCP => Addresses.Length != 0,
            DnsRequestFormat.DnsOverTLS => TlsName != null,
            DnsRequestFormat.DnsOverQuic => Quic,
            DnsRequestFormat.DnsOverHttp3 => Http3,
            DnsRequestFormat.DnsOverHttps or DnsRequestFormat.DnsOverHttpsWirePost => DohPath != null,
            DnsRequestFormat.DnsOverHttpsJSON => JsonPath != null,
            _ => false
        };
        internal DnsResolverEndpoint[] Expand(DnsRequestFormat format) {
            if (!Supports(format)) { throw new NotSupportedException($"This provider profile does not publish {format}."); }
            if (format == DnsRequestFormat.DnsOverUDP || format == DnsRequestFormat.DnsOverTCP) {
                return Addresses.Select(host => new DnsResolverEndpoint {
                    Host = host, Port = 53, Transport = DnsRequestFormatMapper.ToTransport(format), RequestFormat = format
                }).ToArray();
            }
            if (format == DnsRequestFormat.DnsOverTLS || format == DnsRequestFormat.DnsOverQuic) {
                return new[] { new DnsResolverEndpoint {
                    Host = TlsName, TlsServerName = TlsName, Port = 853,
                    Transport = DnsRequestFormatMapper.ToTransport(format), RequestFormat = format
                } };
            }
            return Hosts.Select(host => new DnsResolverEndpoint {
                Host = host, Port = 443, Transport = Transport.Doh, RequestFormat = format,
                DohUrl = new Uri("https://" + host + (format == DnsRequestFormat.DnsOverHttpsJSON ? JsonPath : DohPath))
            }).ToArray();
        }
    }
    private static Profile Doh(string host, string path, string[] addresses, string? tls = null, bool quic = false, bool http3 = false, string? jsonPath = null) =>
        new(new[] { host }, path, DnsRequestFormat.DnsOverHttps, addresses, tls, quic, http3, jsonPath);
    private static Profile Cloudflare(string last, DnsRequestFormat format) => new(
        new[] { "1.1.1." + last, "1.0.0." + last }, "/dns-query", format,
        new[] { "1.1.1." + last, "1.0.0." + last },
        last == "1" ? "one.one.one.one" : last == "2" ? "security.cloudflare-dns.com" : "family.cloudflare-dns.com", jsonPath: "/dns-query");
    private static Profile Quad9(string label, string[] addresses, DnsRequestFormat format = DnsRequestFormat.DnsOverHttps) =>
        new(new[] { label + ".quad9.net" }, "/dns-query", format, addresses, label + ".quad9.net", quic: true, http3: true);
    private static readonly Dictionary<DnsEndpoint, Profile> Profiles = new() {
        [DnsEndpoint.Cloudflare] = Cloudflare("1", DnsRequestFormat.DnsOverHttpsJSON),
        [DnsEndpoint.CloudflareSecurity] = Cloudflare("2", DnsRequestFormat.DnsOverHttpsJSON),
        [DnsEndpoint.CloudflareFamily] = Cloudflare("3", DnsRequestFormat.DnsOverHttpsJSON),
        [DnsEndpoint.CloudflareWireFormat] = Cloudflare("1", DnsRequestFormat.DnsOverHttps),
        [DnsEndpoint.CloudflareWireFormatPost] = Cloudflare("1", DnsRequestFormat.DnsOverHttpsWirePost),
        [DnsEndpoint.Google] = Doh("dns.google", "/dns-query", new[] { "8.8.8.8", "8.8.4.4" }, "dns.google", jsonPath: "/resolve"),
        [DnsEndpoint.GoogleWireFormat] = Doh("dns.google", "/dns-query", new[] { "8.8.8.8", "8.8.4.4" }, "dns.google", jsonPath: "/resolve"),
        [DnsEndpoint.GoogleWireFormatPost] = new(new[] { "dns.google" }, "/dns-query", DnsRequestFormat.DnsOverHttpsWirePost, new[] { "8.8.8.8", "8.8.4.4" }, "dns.google", jsonPath: "/resolve"),
        [DnsEndpoint.Quad9] = Quad9("dns", new[] { "9.9.9.9", "149.112.112.112" }),
        [DnsEndpoint.Quad9Http3] = Quad9("dns", new[] { "9.9.9.9", "149.112.112.112" }, DnsRequestFormat.DnsOverHttp3),
        [DnsEndpoint.Quad9Quic] = Quad9("dns", new[] { "9.9.9.9", "149.112.112.112" }, DnsRequestFormat.DnsOverQuic),
        [DnsEndpoint.Quad9ECS] = Quad9("dns11", new[] { "9.9.9.11", "149.112.112.11" }),
        [DnsEndpoint.Quad9Unsecure] = Quad9("dns10", new[] { "9.9.9.10", "149.112.112.10" }),
        [DnsEndpoint.OpenDNS] = new(new[] { "208.67.222.222", "208.67.220.220" }, "/dns-query", DnsRequestFormat.DnsOverHttps, new[] { "208.67.222.222", "208.67.220.220" }),
        [DnsEndpoint.OpenDNSFamily] = new(new[] { "208.67.222.123", "208.67.220.123" }, "/dns-query", DnsRequestFormat.DnsOverHttps, new[] { "208.67.222.123", "208.67.220.123" }),
        [DnsEndpoint.NextDNS] = new(new[] { "dns.nextdns.io" }, "/dns-query", DnsRequestFormat.DnsOverHttpsJSON, new[] { "45.90.28.0", "45.90.30.0" }),
        [DnsEndpoint.AdGuard] = Doh("dns.adguard-dns.com", "/dns-query", new[] { "94.140.14.14", "94.140.15.15" }, "dns.adguard-dns.com", true),
        [DnsEndpoint.AdGuardFamily] = Doh("family.adguard-dns.com", "/dns-query", new[] { "94.140.14.15", "94.140.15.16" }, "family.adguard-dns.com", true),
        [DnsEndpoint.AdGuardNonFiltering] = Doh("unfiltered.adguard-dns.com", "/dns-query", new[] { "94.140.14.140", "94.140.14.141" }, "unfiltered.adguard-dns.com", true),
        [DnsEndpoint.CleanBrowsingSecurity] = Doh("doh.cleanbrowsing.org", "/doh/security-filter/", new[] { "185.228.168.9", "185.228.169.9" }, "security-filter-dns.cleanbrowsing.org"),
        [DnsEndpoint.CleanBrowsingAdult] = Doh("doh.cleanbrowsing.org", "/doh/adult-filter/", new[] { "185.228.168.10", "185.228.169.11" }, "adult-filter-dns.cleanbrowsing.org"),
        [DnsEndpoint.CleanBrowsingFamily] = Doh("doh.cleanbrowsing.org", "/doh/family-filter/", new[] { "185.228.168.168", "185.228.169.168" }, "family-filter-dns.cleanbrowsing.org"),
        [DnsEndpoint.ControlD] = Doh("freedns.controld.com", "/p0", new[] { "76.76.2.0", "76.76.10.0" }, "p0.freedns.controld.com", true),
        [DnsEndpoint.ControlDMalware] = Doh("freedns.controld.com", "/p1", new[] { "76.76.2.1", "76.76.10.1" }, "p1.freedns.controld.com", true),
        [DnsEndpoint.ControlDAds] = Doh("freedns.controld.com", "/p2", new[] { "76.76.2.2", "76.76.10.2" }, "p2.freedns.controld.com", true),
        [DnsEndpoint.ControlDFamily] = Doh("freedns.controld.com", "/family", new[] { "76.76.2.4", "76.76.10.4" }, "family.freedns.controld.com", true),
        [DnsEndpoint.ComodoSecureDns] = new(new[] { "8.26.56.26", "8.20.247.20" }, null, DnsRequestFormat.DnsOverUDP, new[] { "8.26.56.26", "8.20.247.20" }),
        [DnsEndpoint.DnsWatch] = new(new[] { "84.200.69.80", "84.200.70.40", "2001:1608:10:25::1c04:b12f", "2001:1608:10:25::9249:d69b" }, null, DnsRequestFormat.DnsOverUDP,
            new[] { "84.200.69.80", "84.200.70.40", "2001:1608:10:25::1c04:b12f", "2001:1608:10:25::9249:d69b" })
    };
    internal static Profile? Get(DnsEndpoint endpoint) => Profiles.TryGetValue(endpoint, out Profile? profile) ? profile : null;
}
