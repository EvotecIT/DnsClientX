namespace DnsClientX {
    /// <summary>
    /// Represents a resolver recommendation selected from a persisted score snapshot.
    /// </summary>
    public sealed class ResolverSelectionResult {
        /// <summary>
        /// Gets or sets the score mode that produced the recommendation.
        /// </summary>
        public ResolverScoreMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the raw target string stored in the snapshot.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selection kind.
        /// </summary>
        public ResolverSelectionKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the selected built-in resolver profile when <see cref="Kind"/> is <see cref="ResolverSelectionKind.BuiltInEndpoint"/>.
        /// </summary>
        public DnsEndpoint? BuiltInEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the selected explicit resolver endpoint when <see cref="Kind"/> is <see cref="ResolverSelectionKind.ExplicitEndpoint"/>.
        /// </summary>
        public DnsResolverEndpoint? ExplicitEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the resolver address observed for the recommendation.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the recommendation.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the average latency associated with the recommendation in milliseconds.
        /// </summary>
        public double AverageMs { get; set; }

        /// <summary>
        /// Gets or sets the recommendation source.
        /// </summary>
        public string RecommendationSource { get; set; } = "none";

        /// <summary>
        /// Gets or sets the recommendation status.
        /// </summary>
        public string RecommendationStatus { get; set; } = "none";

        /// <summary>
        /// Gets or sets the recommendation reason.
        /// </summary>
        public string RecommendationReason { get; set; } = "none";
    }
}
