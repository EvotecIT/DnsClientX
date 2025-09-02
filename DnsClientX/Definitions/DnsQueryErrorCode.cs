namespace DnsClientX {
    /// <summary>
    /// Normalized error categories for DNS queries.
    /// </summary>
    public enum DnsQueryErrorCode {
        /// <summary>
        /// No error.
        /// </summary>
        None = 0,
        /// <summary>
        /// Operation timed out.
        /// </summary>
        Timeout,
        /// <summary>
        /// Network-related failure (socket, connectivity, etc.).
        /// </summary>
        Network,
        /// <summary>
        /// Response could not be parsed or was invalid.
        /// </summary>
        InvalidResponse,
        /// <summary>
        /// Generic server failure or unspecified error.
        /// </summary>
        ServFail
    }
}
