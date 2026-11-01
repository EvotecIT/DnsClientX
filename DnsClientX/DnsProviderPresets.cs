namespace DnsClientX {
    /// <summary>Shared public resolver addresses for single-provider and multi-resolver queries.</summary>
    internal static class DnsProviderPresets {
        /// <summary>Returns published hosts and a DoH path, or a null path for plain UDP resolvers.</summary>
        internal static (string[] Hosts, string? DohPath)? Get(DnsEndpoint endpoint) => endpoint switch {
            DnsEndpoint.AdGuard => (new[] { "dns.adguard-dns.com" }, "/dns-query"),
            DnsEndpoint.AdGuardFamily => (new[] { "family.adguard-dns.com" }, "/dns-query"),
            DnsEndpoint.AdGuardNonFiltering => (new[] { "unfiltered.adguard-dns.com" }, "/dns-query"),
            DnsEndpoint.CleanBrowsingSecurity => (new[] { "doh.cleanbrowsing.org" }, "/doh/security-filter/"),
            DnsEndpoint.CleanBrowsingAdult => (new[] { "doh.cleanbrowsing.org" }, "/doh/adult-filter/"),
            DnsEndpoint.CleanBrowsingFamily => (new[] { "doh.cleanbrowsing.org" }, "/doh/family-filter/"),
            DnsEndpoint.ControlD => (new[] { "freedns.controld.com" }, "/p0"),
            DnsEndpoint.ControlDMalware => (new[] { "freedns.controld.com" }, "/p1"),
            DnsEndpoint.ControlDAds => (new[] { "freedns.controld.com" }, "/p2"),
            DnsEndpoint.ControlDFamily => (new[] { "freedns.controld.com" }, "/family"),
            DnsEndpoint.ComodoSecureDns => (new[] { "8.26.56.26", "8.20.247.20" }, null),
            DnsEndpoint.DnsWatch => (new[] {
                "84.200.69.80", "84.200.70.40",
                "2001:1608:10:25::1c04:b12f", "2001:1608:10:25::9249:d69b"
            }, null),
            _ => null
        };
    }
}
