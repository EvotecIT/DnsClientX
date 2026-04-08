namespace DnsClientX {
    /// <summary>
    /// Identifies the workflow that produced a resolver score snapshot.
    /// </summary>
    public enum ResolverScoreMode {
        /// <summary>
        /// The snapshot was produced by a resolver probe workflow.
        /// </summary>
        Probe,

        /// <summary>
        /// The snapshot was produced by a resolver benchmark workflow.
        /// </summary>
        Benchmark
    }
}
