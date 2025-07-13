namespace DnsClientX {
    /// <summary>
    /// Provides extended DNS error information as defined by RFC 8914.
    /// </summary>
    public class ExtendedDnsErrorInfo {
        /// <summary>
        /// Gets or sets the extended DNS error code.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Gets or sets the additional text describing the error.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }
}
