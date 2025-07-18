namespace DnsClientX {
    /// <summary>
    /// Specifies the transport formats supported for DNS queries.
    /// Includes DNS over HTTPS (<a href="https://www.rfc-editor.org/rfc/rfc8484">RFC 8484</a>)
    /// and DNS over TLS (<a href="https://www.rfc-editor.org/rfc/rfc7858">RFC 7858</a>).
    /// </summary>
    /// <remarks>
    /// The selected format determines how <see cref="ClientX"/> sends queries to the remote resolver.
    /// </remarks>
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
        /// Wire format over HTTPS using POST request.
        /// Alias for <see cref="DnsOverHttpsPOST"/> with an explicit name.
        /// </summary>
        DnsOverHttpsWirePost,
        /// <summary>
        /// JSON format for DNS requests sent using POST method.
        /// </summary>
        DnsOverHttpsJSONPOST,
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
        /// <summary>
        /// DNS over QUIC, defined in <a href="https://www.rfc-editor.org/rfc/rfc9250">RFC 9250</a>.
        /// </summary>
        DnsOverQuic,
        /// <summary>
        /// DNS over HTTP/2 using wire format.
        /// </summary>
        DnsOverHttp2,
        /// <summary>
        /// DNS over HTTP/3 using wire format.
        /// </summary>
        DnsOverHttp3,
        /// <summary>
        /// DNS over DNSCrypt using wire format.
        /// </summary>
        DnsCrypt,
        /// <summary>
        /// DNS over DNSCrypt using a relay server.
        /// </summary>
        DnsCryptRelay,
        /// <summary>
        /// Oblivious DNS over HTTPS (RFC 9230).
        /// </summary>
        ObliviousDnsOverHttps,
        /// <summary>
        /// DNS over gRPC using wire format.
        /// </summary>
        DnsOverGrpc,
        /// <summary>
        /// DNS over UDP multicast (mDNS on port 5353).
        /// </summary>
        Multicast,
    }
}
