namespace DnsClientX {
    /// <summary>
    /// Represents a normalized resolver candidate that can be executed by higher-level adapters.
    /// </summary>
    public sealed class ResolverExecutionTarget {
        /// <summary>
        /// Gets or sets the human-readable display name for the candidate.
        /// </summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the built-in endpoint represented by this candidate, when applicable.
        /// </summary>
        public DnsEndpoint? BuiltInEndpoint { get; init; }

        /// <summary>
        /// Gets or sets the explicit resolver endpoint represented by this candidate, when applicable.
        /// </summary>
        public DnsResolverEndpoint? ExplicitEndpoint { get; init; }
    }
}
