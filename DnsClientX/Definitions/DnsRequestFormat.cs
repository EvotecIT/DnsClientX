namespace DnsClientX {
    /// <summary>
    /// Enum representing the available formats for DNS requests.
    /// </summary>
    public enum DnsRequestFormat {
        /// <summary>
        /// JSON format for DNS requests.
        /// </summary>
        JSON,
        /// <summary>
        /// Wire format using GET method for DNS requests.
        /// </summary>
        WireFormatGet,
        /// <summary>
        /// Wire format using POST method for DNS requests.
        /// </summary>
        WireFormatPost,
        /// <summary>
        /// Wire format using the DOT protocol for DNS requests.
        /// </summary>
        WireFormatDot,
    }
}
