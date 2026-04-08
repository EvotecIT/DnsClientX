namespace DnsClientX {
    /// <summary>
    /// Represents one aggregated resolver candidate outcome from a benchmark workflow.
    /// </summary>
    public sealed class ResolverBenchmarkCandidate {
        /// <summary>
        /// Gets or sets the human-readable candidate label.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolver observed on the fastest successful attempt.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed on the fastest successful attempt.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the total query attempts for the candidate.
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
    }
}
