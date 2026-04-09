namespace DnsClientX {
    /// <summary>
    /// Describes one ranked probe candidate in a shared probe report.
    /// </summary>
    public sealed class ResolverProbeReportResult {
        /// <summary>
        /// Gets or sets the candidate label.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolver address observed for the candidate.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the configured request format for the candidate.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; set; }

        /// <summary>
        /// Gets or sets the effective transport observed for the candidate.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the DNS response status for the candidate.
        /// </summary>
        public string Status { get; set; } = "NoResponse";

        /// <summary>
        /// Gets or sets the effective error string for the candidate.
        /// </summary>
        public string Error { get; set; } = "none";

        /// <summary>
        /// Gets or sets the elapsed duration in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Gets or sets the number of answers returned by the candidate.
        /// </summary>
        public int AnswerCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the candidate succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets the ranking position of the candidate.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the candidate was the fastest success.
        /// </summary>
        public bool IsFastestSuccess { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the candidate was the fastest consensus responder.
        /// </summary>
        public bool IsFastestConsensus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the candidate is recommended.
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the overall policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Gets or sets the overall policy reason.
        /// </summary>
        public string PolicyReason { get; set; } = "none";
    }
}
