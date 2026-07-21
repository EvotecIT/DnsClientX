namespace DnsClientX {
    /// <summary>
    /// Identifies where a system DNS configuration was discovered.
    /// </summary>
    public enum SystemDnsDiscoverySource {
        /// <summary>No resolver configuration was discovered.</summary>
        None,

        /// <summary>The configuration came from active network interfaces.</summary>
        NetworkInterfaces,

        /// <summary>The configuration came from a resolv.conf file.</summary>
        ResolvConf,

        /// <summary>The configuration was supplied by an explicit public-resolver fallback.</summary>
        PublicFallback,

        /// <summary>The configuration came from a caller-provided discovery source.</summary>
        CustomProvider
    }
}
