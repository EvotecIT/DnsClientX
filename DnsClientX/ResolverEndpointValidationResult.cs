namespace DnsClientX {
    /// <summary>
    /// Describes validation outcome for one resolver endpoint catalog entry.
    /// </summary>
    public sealed class ResolverEndpointValidationResult {
        /// <summary>
        /// Source of the entry, such as inline input, file path, or URL.
        /// </summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// One-based line number when the entry came from line-oriented content.
        /// </summary>
        public int? LineNumber { get; init; }

        /// <summary>
        /// Original endpoint entry.
        /// </summary>
        public string Entry { get; init; } = string.Empty;

        /// <summary>
        /// Indicates whether the entry parsed successfully.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        /// Parsed endpoint when validation succeeded.
        /// </summary>
        public DnsResolverEndpoint? Endpoint { get; init; }

        /// <summary>
        /// Validation error when parsing failed.
        /// </summary>
        public string? Error { get; init; }
    }
}
