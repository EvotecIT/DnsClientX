namespace DnsClientX {
    /// <summary>
    /// Enum representing the available DNS-over-HTTPS endpoints.
    /// </summary>
    public enum DnsEndpoint {
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
        //CloudflareWireFormatPost,
        /// <summary>
        /// Google's DNS-over-HTTPS endpoint.
        /// </summary>
        Google,
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
