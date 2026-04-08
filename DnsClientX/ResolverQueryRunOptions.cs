namespace DnsClientX {
    /// <summary>
    /// Configures shared resolver query execution for probe and benchmark workflows.
    /// </summary>
    public sealed class ResolverQueryRunOptions {
        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; init; } = Configuration.DefaultTimeout;

        /// <summary>
        /// Gets or sets a value indicating whether DNSSEC data should be requested.
        /// </summary>
        public bool RequestDnsSec { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether DNSSEC responses should be validated.
        /// </summary>
        public bool ValidateDnsSec { get; init; }

        /// <summary>
        /// Gets or sets the retry count for each query attempt.
        /// </summary>
        public int MaxRetries { get; init; } = 1;

        /// <summary>
        /// Gets or sets the retry delay in milliseconds.
        /// </summary>
        public int RetryDelayMs { get; init; }

        /// <summary>
        /// Gets or sets an optional port override applied when creating per-target clients.
        /// </summary>
        public int? PortOverride { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether DoH targets should be forced to wire-format POST requests.
        /// </summary>
        public bool ForceDohWirePost { get; init; }
    }
}
