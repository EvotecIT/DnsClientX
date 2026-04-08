namespace DnsClientX {
    /// <summary>
    /// Represents a fully shaped shared probe report.
    /// </summary>
    public sealed class ResolverProbeReport {
        /// <summary>Gets or sets the ranked probe results.</summary>
        public ResolverProbeReportResult[] Results { get; set; } = System.Array.Empty<ResolverProbeReportResult>();
        /// <summary>Gets or sets the probe summary.</summary>
        public ResolverProbeReportSummary Summary { get; set; } = new ResolverProbeReportSummary();
        /// <summary>Gets or sets the underlying evaluation used for the report.</summary>
        public ResolverProbeEvaluation Evaluation { get; set; } = new ResolverProbeEvaluation();
        /// <summary>Gets or sets the persisted score snapshot for the report.</summary>
        public ResolverScoreSnapshot Snapshot { get; set; } = new ResolverScoreSnapshot();
    }
}
