namespace DnsClientX {
    /// <summary>
    /// Represents one scored resolver candidate within a persisted probe or benchmark snapshot.
    /// </summary>
    public sealed class ResolverScoreEntry {
        /// <summary>
        /// Gets or sets the human-readable candidate label.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolver address observed for the candidate.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the candidate.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the total number of queries executed for the candidate.
        /// </summary>
        public int TotalQueries { get; set; }

        /// <summary>
        /// Gets or sets the successful query count for the candidate.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the failed query count for the candidate.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the successful query percentage for the candidate.
        /// </summary>
        public int SuccessPercent { get; set; }

        /// <summary>
        /// Gets or sets the average successful latency in milliseconds.
        /// </summary>
        public double AverageMs { get; set; }

        /// <summary>
        /// Gets or sets the minimum successful latency in milliseconds.
        /// </summary>
        public double MinMs { get; set; }

        /// <summary>
        /// Gets or sets the maximum successful latency in milliseconds.
        /// </summary>
        public double MaxMs { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct answer sets observed for the candidate.
        /// </summary>
        public int DistinctAnswerSets { get; set; }

        /// <summary>
        /// Gets or sets the ranking position for the candidate within the snapshot.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the candidate was the top-ranked successful result.
        /// </summary>
        public bool IsBest { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the candidate is recommended under the current policy.
        /// </summary>
        public bool IsRecommended { get; set; }
    }
}
