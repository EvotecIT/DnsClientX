namespace DnsClientX {
    /// <summary>
    /// Enumerates known DNS endpoints including DNS-over-HTTPS and DNSCrypt
    /// providers as well as system resolvers. DNS-over-HTTPS is defined in
    /// <a href="https://www.rfc-editor.org/rfc/rfc8484">RFC 8484</a>.
    /// </summary>
    public enum DnsEndpoint {
        /// <summary>
        /// Use the system's default DNS resolver using UDP. When using this option, the system's default DNS resolver will be used.
        /// When UDP reaches the maximum packet size, it will automatically switch to TCP.
        /// </summary>
        System,
        /// <summary>
        /// Use the system's default DNS resolver using TCP. When using this option, the system's default DNS resolver will be used.
        /// </summary>
        SystemTcp,
        /// <summary>
        /// Cloudflare DNS-over-HTTPS endpoint.
        /// </summary>
        Cloudflare,
        /// <summary>
        /// Cloudflare's security-focused DNS-over-HTTPS endpoint.
        /// </summary>
        CloudflareSecurity,
        /// <summary>
        /// Cloudflare's family-friendly DNS-over-HTTPS endpoint.
        /// </summary>
        CloudflareFamily,
        /// <summary>
        /// Cloudflare's DNS-over-HTTPS endpoint using wire format.
        /// </summary>
        CloudflareWireFormat,
        /// <summary>
        /// Cloudflare's DNS-over-HTTPS endpoint using wire format with POST method.
        /// </summary>
        CloudflareWireFormatPost,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint.
        /// </summary>
        Google,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint using wire format over GET method.
        /// </summary>
        GoogleWireFormat,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint using wire format over POST method.
        /// </summary>
        GoogleWireFormatPost,
        /// <summary>
        /// Quad9's DNS-over-HTTPS endpoint.
        /// </summary>
        Quad9,
        /// <summary>
        /// Quad9's DNS-over-HTTPS endpoint with ECS support.
        /// </summary>
        Quad9ECS,
        /// <summary>
        /// Quad9's unsecured DNS-over-HTTPS endpoint.
        /// </summary>
        Quad9Unsecure,
        /// <summary>
        /// OpenDNS's DNS-over-HTTPS endpoint.
        /// </summary>
        OpenDNS,
        /// <summary>
        /// OpenDNS's family-friendly DNS-over-HTTPS endpoint.
        /// </summary>
        OpenDNSFamily,
        /// <summary>
        /// Cloudflare's DNS-over-QUIC endpoint.
        /// </summary>
        CloudflareQuic,
        /// <summary>
        /// Google's DNS-over-QUIC endpoint.
        /// </summary>
        GoogleQuic,
        /// <summary>
        /// AdGuard DNS-over-HTTPS endpoint.
        /// </summary>
        AdGuard,
        /// <summary>
        /// AdGuard family protection DNS-over-HTTPS endpoint.
        /// </summary>
        AdGuardFamily,
        /// <summary>
        /// AdGuard non-filtering DNS-over-HTTPS endpoint.
        /// </summary>
        AdGuardNonFiltering,
        /// <summary>
        /// Cloudflare DNSCrypt endpoint.
        /// </summary>
        DnsCryptCloudflare,
        /// <summary>
        /// Quad9 DNSCrypt endpoint.
        /// </summary>
        DnsCryptQuad9,
        /// <summary>
        /// DNSCrypt relay server option.
        /// </summary>
        DnsCryptRelay,
        /// <summary>
        /// DNS root servers, queried iteratively starting from one of the
        /// well known A-M root server instances.
        /// </summary>
        RootServer,
        /// <summary>
        /// Cloudflare's Oblivious DNS-over-HTTPS endpoint.
        /// </summary>
        CloudflareOdoh,
        /// <summary>
        /// Custom DNS endpoint configured via <see cref="Configuration"/>
        /// overrides.
        /// </summary>
        Custom
    }
}
