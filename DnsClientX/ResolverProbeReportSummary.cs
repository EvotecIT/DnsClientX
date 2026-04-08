namespace DnsClientX {
    /// <summary>
    /// Describes the shared summary of a probe run.
    /// </summary>
    public sealed class ResolverProbeReportSummary {
        /// <summary>Gets or sets the probed DNS name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the probed record type.</summary>
        public DnsRecordType RecordType { get; set; }
        /// <summary>Gets or sets the per-query timeout in milliseconds.</summary>
        public int TimeoutMs { get; set; }
        /// <summary>Gets or sets the total candidate count.</summary>
        public int CandidateCount { get; set; }
        /// <summary>Gets or sets the number of successful candidates.</summary>
        public int SuccessfulCandidates { get; set; }
        /// <summary>Gets or sets the number of failed candidates.</summary>
        public int FailedCandidates { get; set; }
        /// <summary>Gets or sets the successful candidate percentage.</summary>
        public int SuccessPercent { get; set; }
        /// <summary>Gets or sets a value indicating whether the overall policy passed.</summary>
        public bool PolicyPassed { get; set; }
        /// <summary>Gets or sets the overall policy reason.</summary>
        public string PolicyReason { get; set; } = "none";
        /// <summary>Gets or sets the required minimum success count, when configured.</summary>
        public int? RequiredMinSuccessCount { get; set; }
        /// <summary>Gets or sets the required minimum success percentage, when configured.</summary>
        public int? RequiredMinSuccessPercent { get; set; }
        /// <summary>Gets or sets the required minimum consensus percentage, when configured.</summary>
        public int? RequiredMinConsensusPercent { get; set; }
        /// <summary>Gets or sets a value indicating whether consensus was required.</summary>
        public bool RequireConsensus { get; set; }
        /// <summary>Gets or sets the number of distinct answer sets.</summary>
        public int DistinctAnswerSets { get; set; }
        /// <summary>Gets or sets the size of the leading consensus group.</summary>
        public int ConsensusCount { get; set; }
        /// <summary>Gets or sets the number of successful candidates considered for consensus.</summary>
        public int ConsensusTotal { get; set; }
        /// <summary>Gets or sets the consensus percentage.</summary>
        public int ConsensusPercent { get; set; }
        /// <summary>Gets or sets the fastest successful target.</summary>
        public string FastestSuccessTarget { get; set; } = "none";
        /// <summary>Gets or sets the resolver of the fastest successful target.</summary>
        public string FastestSuccessResolver { get; set; } = "none";
        /// <summary>Gets or sets the transport of the fastest successful target.</summary>
        public string FastestSuccessTransport { get; set; } = "none";
        /// <summary>Gets or sets the latency of the fastest successful target in milliseconds.</summary>
        public double FastestSuccessMs { get; set; }
        /// <summary>Gets or sets the fastest consensus target.</summary>
        public string FastestConsensusTarget { get; set; } = "none";
        /// <summary>Gets or sets the resolver of the fastest consensus target.</summary>
        public string FastestConsensusResolver { get; set; } = "none";
        /// <summary>Gets or sets the transport of the fastest consensus target.</summary>
        public string FastestConsensusTransport { get; set; } = "none";
        /// <summary>Gets or sets the latency of the fastest consensus target in milliseconds.</summary>
        public double FastestConsensusMs { get; set; }
        /// <summary>Gets or sets a value indicating whether a recommendation is available.</summary>
        public bool RecommendationAvailable { get; set; }
        /// <summary>Gets or sets the recommended target.</summary>
        public string RecommendedTarget { get; set; } = "none";
        /// <summary>Gets or sets the recommended resolver.</summary>
        public string RecommendedResolver { get; set; } = "none";
        /// <summary>Gets or sets the recommended transport.</summary>
        public string RecommendedTransport { get; set; } = "none";
        /// <summary>Gets or sets the recommended latency in milliseconds.</summary>
        public double RecommendedAverageMs { get; set; }
        /// <summary>Gets or sets the recommendation source.</summary>
        public string RecommendationSource { get; set; } = "none";
        /// <summary>Gets or sets the recommendation status.</summary>
        public string RecommendationStatus { get; set; } = "none";
        /// <summary>Gets or sets the recommendation reason.</summary>
        public string RecommendationReason { get; set; } = "none";
        /// <summary>Gets or sets the transport coverage entries.</summary>
        public ResolverProbeTransportCoverage[] TransportCoverage { get; set; } = System.Array.Empty<ResolverProbeTransportCoverage>();
        /// <summary>Gets or sets the number of unique probe candidates blocked by runtime transport support.</summary>
        public int RuntimeUnsupportedCandidateCount { get; set; }
        /// <summary>Gets or sets the runtime capability warnings observed in the probe run.</summary>
        public string[] RuntimeCapabilityWarnings { get; set; } = System.Array.Empty<string>();
        /// <summary>Gets or sets the targets outside the leading consensus group.</summary>
        public string[] MismatchedTargets { get; set; } = System.Array.Empty<string>();
        /// <summary>Gets or sets the distinct successful answer variants.</summary>
        public ResolverProbeAnswerVariant[] AnswerVariants { get; set; } = System.Array.Empty<ResolverProbeAnswerVariant>();
    }
}
