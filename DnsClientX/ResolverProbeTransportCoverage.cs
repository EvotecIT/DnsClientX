namespace DnsClientX {
    /// <summary>
    /// Describes successful and total probe coverage for one transport.
    /// </summary>
    public sealed class ResolverProbeTransportCoverage {
        /// <summary>
        /// Gets or sets the transport name.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the number of successful probe attempts on this transport.
        /// </summary>
        public int SuccessfulCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of probe attempts on this transport.
        /// </summary>
        public int TotalCount { get; set; }
    }
}
