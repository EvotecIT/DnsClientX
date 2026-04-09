namespace DnsClientX {
    /// <summary>
    /// Represents one ranked resolver candidate in a probe evaluation.
    /// </summary>
    public sealed class ResolverProbeEvaluationEntry {
        /// <summary>
        /// Gets or sets the human-readable candidate label.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolver address observed for the candidate.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the observed or inferred transport for the candidate.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the elapsed probe time in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the probe succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets the ranking position for the candidate.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this candidate was the fastest successful responder.
        /// </summary>
        public bool IsFastestSuccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this candidate was the fastest consensus responder.
        /// </summary>
        public bool IsFastestConsensus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this candidate is recommended.
        /// </summary>
        public bool IsRecommended { get; set; }
    }
}
