namespace DnsClientX {
    /// <summary>
    /// Provides extended DNS error information as defined by RFC 8914.
    /// </summary>
    /// <remarks>
    /// Servers may include this structure when additional diagnostics
    /// are available for a failed query.
    /// </remarks>
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
