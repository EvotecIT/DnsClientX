namespace DnsClientX {
    /// <summary>
    /// Specifies the transport formats supported for DNS queries.
    /// Includes DNS over HTTPS (<a href="https://www.rfc-editor.org/rfc/rfc8484">RFC 8484</a>)
    /// and DNS over TLS (<a href="https://www.rfc-editor.org/rfc/rfc7858">RFC 7858</a>).
    /// </summary>
    public enum DnsRequestFormat {
        /// <summary>
        /// Wire format using GET method for DNS requests.
        /// </summary>
        DnsOverHttps,
        /// <summary>
        /// JSON format for DNS requests.
        /// </summary>
        DnsOverHttpsJSON,
        /// <summary>
        /// Wire format using POST method for DNS requests.
        /// </summary>
        DnsOverHttpsPOST,
        /// <summary>
        /// Format for DNS requests using UDP.
        /// </summary>
        DnsOverUDP,
        /// <summary>
        /// Format for DNS requests using TCP.
        /// </summary>
        DnsOverTCP,
        /// <summary>
        /// Wire format using the DOT protocol for DNS requests.
        /// </summary>
        DnsOverTLS,
    }
}
