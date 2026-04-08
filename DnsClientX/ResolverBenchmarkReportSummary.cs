namespace DnsClientX {
    /// <summary>
    /// Describes the shared summary of a benchmark run.
    /// </summary>
    public sealed class ResolverBenchmarkReportSummary {
        /// <summary>Gets or sets the benchmark domains.</summary>
        public string[] Domains { get; set; } = System.Array.Empty<string>();
        /// <summary>Gets or sets the benchmark record types.</summary>
        public DnsRecordType[] RecordTypes { get; set; } = System.Array.Empty<DnsRecordType>();
        /// <summary>Gets or sets the attempts per combination.</summary>
        public int AttemptsPerCombination { get; set; }
        /// <summary>Gets or sets the max concurrency.</summary>
        public int MaxConcurrency { get; set; }
        /// <summary>Gets or sets the timeout in milliseconds.</summary>
        public int TimeoutMs { get; set; }
        /// <summary>Gets or sets the candidate count.</summary>
        public int CandidateCount { get; set; }
        /// <summary>Gets or sets the successful candidate count.</summary>
        public int SuccessfulCandidates { get; set; }
        /// <summary>Gets or sets the overall successful query count.</summary>
        public int OverallSuccessCount { get; set; }
        /// <summary>Gets or sets the overall query count.</summary>
        public int OverallQueryCount { get; set; }
        /// <summary>Gets or sets the overall successful query percentage.</summary>
        public int OverallSuccessPercent { get; set; }
        /// <summary>Gets or sets a value indicating whether the overall policy passed.</summary>
        public bool PolicyPassed { get; set; }
        /// <summary>Gets or sets the overall policy reason.</summary>
        public string PolicyReason { get; set; } = "none";
        /// <summary>Gets or sets the required minimum success percentage.</summary>
        public int? RequiredMinSuccessPercent { get; set; }
        /// <summary>Gets or sets the required minimum successful candidate count.</summary>
        public int? RequiredMinSuccessfulCandidates { get; set; }
        /// <summary>Gets or sets the recommended target.</summary>
        public string RecommendedTarget { get; set; } = "none";
        /// <summary>Gets or sets the recommended resolver.</summary>
        public string RecommendedResolver { get; set; } = "none";
        /// <summary>Gets or sets the recommended transport.</summary>
        public string RecommendedTransport { get; set; } = "none";
        /// <summary>Gets or sets the recommended average latency in milliseconds.</summary>
        public double RecommendedAverageMs { get; set; }
        /// <summary>Gets or sets a value indicating whether a recommendation is available.</summary>
        public bool RecommendationAvailable { get; set; }
    }
}
