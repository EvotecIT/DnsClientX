using System;

namespace DnsClientX {
    /// <summary>
    /// Describes the run-level outcome for a persisted resolver score snapshot.
    /// </summary>
    public sealed class ResolverScoreSummary {
        /// <summary>
        /// Gets or sets the workflow that produced the snapshot.
        /// </summary>
        public ResolverScoreMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the domain names included in the scoring run.
        /// </summary>
        public string[] Domains { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the record types included in the scoring run.
        /// </summary>
        public DnsRecordType[] RecordTypes { get; set; } = Array.Empty<DnsRecordType>();

        /// <summary>
        /// Gets or sets the number of attempts executed for each domain and type combination.
        /// </summary>
        public int AttemptsPerCombination { get; set; }

        /// <summary>
        /// Gets or sets the maximum concurrency used for the run.
        /// </summary>
        public int MaxConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the per-query timeout in milliseconds used for the run.
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the total candidate count in the run.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Gets or sets the number of candidates with at least one successful response.
        /// </summary>
        public int SuccessfulCandidates { get; set; }

        /// <summary>
        /// Gets or sets the total successful query count across all candidates.
        /// </summary>
        public int OverallSuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the total query count across all candidates.
        /// </summary>
        public int OverallQueryCount { get; set; }

        /// <summary>
        /// Gets or sets the overall successful query percentage across all candidates.
        /// </summary>
        public int OverallSuccessPercent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the run-level policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Gets or sets the policy failure reason, or <c>none</c> when the policy passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";

        /// <summary>
        /// Gets or sets the required minimum success percentage, if any.
        /// </summary>
        public int? RequiredMinSuccessPercent { get; set; }

        /// <summary>
        /// Gets or sets the required minimum successful candidate count, if any.
        /// </summary>
        public int? RequiredMinSuccessfulCandidates { get; set; }

        /// <summary>
        /// Gets or sets the required minimum successful probe count, if any.
        /// </summary>
        public int? RequiredMinSuccessCount { get; set; }

        /// <summary>
        /// Gets or sets the required minimum consensus percentage, if any.
        /// </summary>
        public int? RequiredMinConsensusPercent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether full answer consensus was required.
        /// </summary>
        public bool RequireConsensus { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a recommendation is available.
        /// </summary>
        public bool RecommendationAvailable { get; set; }

        /// <summary>
        /// Gets or sets the recommended candidate label.
        /// </summary>
        public string RecommendedTarget { get; set; } = "none";

        /// <summary>
        /// Gets or sets the resolver observed for the recommended candidate.
        /// </summary>
        public string RecommendedResolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the recommended candidate.
        /// </summary>
        public string RecommendedTransport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the average latency of the recommended candidate in milliseconds.
        /// </summary>
        public double RecommendedAverageMs { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct answer sets observed across successful candidates.
        /// </summary>
        public int DistinctAnswerSets { get; set; }

        /// <summary>
        /// Gets or sets the size of the leading consensus group.
        /// </summary>
        public int ConsensusCount { get; set; }

        /// <summary>
        /// Gets or sets the number of successful responses considered for consensus.
        /// </summary>
        public int ConsensusTotal { get; set; }

        /// <summary>
        /// Gets or sets the consensus percentage for the leading answer group.
        /// </summary>
        public int ConsensusPercent { get; set; }

        /// <summary>
        /// Gets or sets the fastest successful candidate label.
        /// </summary>
        public string FastestSuccessTarget { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the fastest successful candidate.
        /// </summary>
        public string FastestSuccessTransport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the latency of the fastest successful candidate in milliseconds.
        /// </summary>
        public double FastestSuccessMs { get; set; }

        /// <summary>
        /// Gets or sets the fastest candidate within the leading consensus group.
        /// </summary>
        public string FastestConsensusTarget { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the fastest consensus candidate.
        /// </summary>
        public string FastestConsensusTransport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the latency of the fastest consensus candidate in milliseconds.
        /// </summary>
        public double FastestConsensusMs { get; set; }

        /// <summary>
        /// Gets or sets the source used to derive the recommendation.
        /// </summary>
        public string RecommendationSource { get; set; } = "none";

        /// <summary>
        /// Gets or sets the recommendation status.
        /// </summary>
        public string RecommendationStatus { get; set; } = "none";

        /// <summary>
        /// Gets or sets the reason why no recommendation was produced, or <c>none</c>.
        /// </summary>
        public string RecommendationReason { get; set; } = "none";
    }
}
