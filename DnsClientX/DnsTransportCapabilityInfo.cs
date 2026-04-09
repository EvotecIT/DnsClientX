namespace DnsClientX {
    /// <summary>
    /// Describes runtime support for one DNS transport exposed by the core library.
    /// </summary>
    public sealed class DnsTransportCapabilityInfo {
        /// <summary>
        /// Gets or sets the display name of the transport.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request format represented by the transport.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the transport is supported on the current runtime.
        /// </summary>
        public bool Supported { get; set; }

        /// <summary>
        /// Gets or sets the package that exposes the transport.
        /// </summary>
        public string Package { get; set; } = "DnsClientX";

        /// <summary>
        /// Gets or sets the target-framework scope for the transport.
        /// </summary>
        public string TargetFrameworkScope { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets runtime-specific requirements for the transport.
        /// </summary>
        public string RuntimeRequirement { get; set; } = "none";

        /// <summary>
        /// Gets or sets an explanatory note for the transport.
        /// </summary>
        public string Notes { get; set; } = string.Empty;
    }
}
