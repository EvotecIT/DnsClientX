namespace DnsClientX {
    /// <summary>
    /// Enum representing the available formats for DNS requests.
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
