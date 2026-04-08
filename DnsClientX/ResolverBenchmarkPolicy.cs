namespace DnsClientX {
    /// <summary>
    /// Describes policy gates applied to benchmark candidates.
    /// </summary>
    public sealed class ResolverBenchmarkPolicy {
        /// <summary>
        /// Gets or sets the required minimum overall success percentage, if any.
        /// </summary>
        public int? MinSuccessPercent { get; set; }

        /// <summary>
        /// Gets or sets the required minimum successful candidate count, if any.
        /// </summary>
        public int? MinSuccessfulCandidates { get; set; }
    }
}
