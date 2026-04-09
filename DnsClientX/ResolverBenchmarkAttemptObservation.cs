namespace DnsClientX {
    /// <summary>
    /// Represents one individual benchmark attempt observation before candidate aggregation.
    /// </summary>
    public sealed class ResolverBenchmarkAttemptObservation {
        /// <summary>
        /// Gets or sets the resolver observed for the attempt.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the attempt.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the elapsed benchmark time in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the attempt succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets the stable answer signature used to count distinct answer sets.
        /// </summary>
        public string AnswerSignature { get; set; } = "(no answers)";
    }
}
