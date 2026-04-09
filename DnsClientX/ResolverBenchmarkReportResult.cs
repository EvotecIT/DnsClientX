namespace DnsClientX {
    /// <summary>
    /// Describes one ranked benchmark candidate in a shared benchmark report.
    /// </summary>
    public sealed class ResolverBenchmarkReportResult {
        /// <summary>Gets or sets the candidate label.</summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>Gets or sets the resolver observed for the candidate.</summary>
        public string Resolver { get; set; } = "none";
        /// <summary>Gets or sets the observed transport for the candidate.</summary>
        public string Transport { get; set; } = "none";
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
        /// <summary>Gets or sets the total query count.</summary>
        public int TotalQueries { get; set; }
        /// <summary>Gets or sets the successful query count.</summary>
        public int SuccessCount { get; set; }
        /// <summary>Gets or sets the failed query count.</summary>
        public int FailureCount { get; set; }
        /// <summary>Gets or sets the successful query percentage.</summary>
        public int SuccessPercent { get; set; }
        /// <summary>Gets or sets the average latency in milliseconds.</summary>
        public double AverageMs { get; set; }
        /// <summary>Gets or sets the minimum latency in milliseconds.</summary>
        public double MinMs { get; set; }
        /// <summary>Gets or sets the maximum latency in milliseconds.</summary>
        public double MaxMs { get; set; }
        /// <summary>Gets or sets the number of distinct answer sets.</summary>
        public int DistinctAnswerSets { get; set; }
        /// <summary>Gets or sets the ranking position.</summary>
        public int Rank { get; set; }
        /// <summary>Gets or sets a value indicating whether the candidate is the best successful result.</summary>
        public bool IsBest { get; set; }
        /// <summary>Gets or sets a value indicating whether the candidate is recommended.</summary>
        public bool IsRecommended { get; set; }
        /// <summary>Gets or sets a value indicating whether the overall policy passed.</summary>
        public bool PolicyPassed { get; set; }
        /// <summary>Gets or sets the overall policy reason.</summary>
        public string PolicyReason { get; set; } = "none";
        /// <summary>Gets or sets the required minimum success percentage.</summary>
        public int? RequiredMinSuccessPercent { get; set; }
        /// <summary>Gets or sets the required minimum successful candidate count.</summary>
        public int? RequiredMinSuccessfulCandidates { get; set; }
        /// <summary>Gets or sets the total candidate count.</summary>
        public int CandidateCount { get; set; }
        /// <summary>Gets or sets the successful candidate count.</summary>
        public int SuccessfulCandidates { get; set; }
        /// <summary>Gets or sets the overall successful query percentage.</summary>
        public int OverallSuccessPercent { get; set; }
        /// <summary>Gets or sets the overall successful query count.</summary>
        public int OverallSuccessCount { get; set; }
        /// <summary>Gets or sets the overall query count.</summary>
        public int OverallQueryCount { get; set; }
    }
}
