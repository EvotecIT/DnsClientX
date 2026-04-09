namespace DnsClientX {
    /// <summary>
    /// Represents one ranked benchmark candidate in a benchmark evaluation.
    /// </summary>
    public sealed class ResolverBenchmarkEvaluationEntry {
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

        /// <summary>
        /// Gets or sets the ranking position for the candidate.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this candidate was the best successful result.
        /// </summary>
        public bool IsBest { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this candidate is recommended.
        /// </summary>
        public bool IsRecommended { get; set; }
    }
}
