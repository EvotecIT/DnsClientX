namespace DnsClientX {
    /// <summary>
    /// Configures shared single-target client creation for adapter-driven operations.
    /// </summary>
    public sealed class ResolverExecutionClientOptions {
        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// </summary>
        public int? TimeoutMs { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether audit logging should be enabled.
        /// </summary>
        public bool EnableAudit { get; init; }

        /// <summary>
        /// Gets or sets an optional port override applied after client creation.
        /// </summary>
        public int? PortOverride { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether DoH requests should prefer wire-format POST when supported.
        /// </summary>
        public bool ForceDohWirePost { get; init; }
    }
}
