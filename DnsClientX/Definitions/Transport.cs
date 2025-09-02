namespace DnsClientX {
    /// <summary>
    /// DNS transport protocol used to reach a resolver endpoint.
    /// </summary>
    public enum Transport {
        /// <summary>
        /// DNS over UDP (port 53).
        /// </summary>
        Udp,
        /// <summary>
        /// DNS over TCP (port 53).
        /// </summary>
        Tcp,
        /// <summary>
        /// DNS over TLS (RFC 7858, typically port 853).
        /// </summary>
        Dot,
        /// <summary>
        /// DNS over HTTPS (RFC 8484, HTTPS endpoints).
        /// </summary>
        Doh
    }
}
