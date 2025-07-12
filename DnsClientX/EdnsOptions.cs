namespace DnsClientX {
    /// <summary>
    /// Represents EDNS options used when sending DNS queries.
    /// </summary>
    public class EdnsOptions {
        /// <summary>
        /// Gets or sets a value indicating whether EDNS should be enabled.
        /// </summary>
        public bool EnableEdns { get; set; } = true;

        /// <summary>
        /// Gets or sets the UDP buffer size used for EDNS queries.
        /// </summary>
        public int UdpBufferSize { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the EDNS Client Subnet (ECS) in CIDR notation.
        /// </summary>
        public string? Subnet { get; set; }

        /// <summary>
        /// Gets the additional EDNS options to include in the OPT record.
        /// </summary>
        public System.Collections.Generic.List<EdnsOption> Options { get; } = [];
    }
}
