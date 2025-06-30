namespace DnsClientX {
    /// <summary>
    /// Enumerates known DNS-over-HTTPS endpoints and system resolvers.
    /// DNS-over-HTTPS is defined in <a href="https://www.rfc-editor.org/rfc/rfc8484">RFC 8484</a>.
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
        OpenDNSFamily
    }
}
