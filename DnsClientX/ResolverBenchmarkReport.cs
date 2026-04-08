namespace DnsClientX {
    /// <summary>
    /// Represents a fully shaped shared benchmark report.
    /// </summary>
    public sealed class ResolverBenchmarkReport {
        /// <summary>Gets or sets the ranked benchmark results.</summary>
        public ResolverBenchmarkReportResult[] Results { get; set; } = System.Array.Empty<ResolverBenchmarkReportResult>();
        /// <summary>Gets or sets the benchmark summary.</summary>
        public ResolverBenchmarkReportSummary Summary { get; set; } = new ResolverBenchmarkReportSummary();
        /// <summary>Gets or sets the underlying benchmark evaluation.</summary>
        public ResolverBenchmarkEvaluation Evaluation { get; set; } = new ResolverBenchmarkEvaluation();
        /// <summary>Gets or sets the persisted score snapshot for the report.</summary>
        public ResolverScoreSnapshot Snapshot { get; set; } = new ResolverScoreSnapshot();
    }
}
