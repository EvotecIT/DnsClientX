using System;

namespace DnsClientX {
    /// <summary>
    /// Describes the ranked outcome of a probe evaluation.
    /// </summary>
    public sealed class ResolverProbeEvaluation {
        /// <summary>
        /// Gets or sets the ranked probe evaluation entries.
        /// </summary>
        public ResolverProbeEvaluationEntry[] Results { get; set; } = Array.Empty<ResolverProbeEvaluationEntry>();

        /// <summary>
        /// Gets or sets the total candidate count in the evaluation.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Gets or sets the number of successful candidates in the evaluation.
        /// </summary>
        public int SuccessfulCandidates { get; set; }

        /// <summary>
        /// Gets or sets the successful candidate percentage across the evaluation.
        /// </summary>
        public int SuccessPercent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the evaluation policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Gets or sets the policy failure reason, or <c>none</c> when the policy passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";

        /// <summary>
        /// Gets or sets the number of distinct answer sets across successful candidates.
        /// </summary>
        public int DistinctAnswerSets { get; set; }

        /// <summary>
        /// Gets or sets the size of the leading consensus group.
        /// </summary>
        public int ConsensusCount { get; set; }

        /// <summary>
        /// Gets or sets the number of successful candidates considered for consensus.
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
        /// Gets or sets the resolver observed for the fastest successful candidate.
        /// </summary>
        public string FastestSuccessResolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the fastest successful candidate.
        /// </summary>
        public string FastestSuccessTransport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the latency of the fastest successful candidate in milliseconds.
        /// </summary>
        public double FastestSuccessMs { get; set; }

        /// <summary>
        /// Gets or sets the fastest consensus candidate label.
        /// </summary>
        public string FastestConsensusTarget { get; set; } = "none";

        /// <summary>
        /// Gets or sets the resolver observed for the fastest consensus candidate.
        /// </summary>
        public string FastestConsensusResolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed for the fastest consensus candidate.
        /// </summary>
        public string FastestConsensusTransport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the latency of the fastest consensus candidate in milliseconds.
        /// </summary>
        public double FastestConsensusMs { get; set; }

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
        /// Gets or sets the latency of the recommended candidate in milliseconds.
        /// </summary>
        public double RecommendedAverageMs { get; set; }

        /// <summary>
        /// Gets or sets the source used to derive the recommendation.
        /// </summary>
        public string RecommendationSource { get; set; } = "none";

        /// <summary>
        /// Gets or sets the recommendation availability status.
        /// </summary>
        public string RecommendationStatus { get; set; } = "none";

        /// <summary>
        /// Gets or sets the reason why no recommendation was produced, or <c>none</c>.
        /// </summary>
        public string RecommendationReason { get; set; } = "none";

        /// <summary>
        /// Creates a persisted resolver score snapshot from the evaluation.
        /// </summary>
        public ResolverScoreSnapshot CreateSnapshot(ResolverProbePolicy policy, string[] domains, DnsRecordType[] recordTypes, int timeoutMs) {
            return new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Probe,
                    Domains = domains ?? Array.Empty<string>(),
                    RecordTypes = recordTypes ?? Array.Empty<DnsRecordType>(),
                    AttemptsPerCombination = 1,
                    TimeoutMs = timeoutMs,
                    CandidateCount = CandidateCount,
                    SuccessfulCandidates = SuccessfulCandidates,
                    OverallSuccessCount = SuccessfulCandidates,
                    OverallQueryCount = CandidateCount,
                    OverallSuccessPercent = SuccessPercent,
                    PolicyPassed = PolicyPassed,
                    PolicyReason = PolicyReason,
                    RequiredMinSuccessCount = policy?.MinSuccessCount,
                    RequiredMinSuccessPercent = policy?.MinSuccessPercent,
                    RequiredMinConsensusPercent = policy?.MinConsensusPercent,
                    RequireConsensus = policy?.RequireConsensus == true,
                    RecommendationAvailable = RecommendationAvailable,
                    RecommendedTarget = RecommendedTarget,
                    RecommendedResolver = RecommendedResolver,
                    RecommendedTransport = RecommendedTransport,
                    RecommendedAverageMs = RecommendedAverageMs,
                    DistinctAnswerSets = DistinctAnswerSets,
                    ConsensusCount = ConsensusCount,
                    ConsensusTotal = ConsensusTotal,
                    ConsensusPercent = ConsensusPercent,
                    FastestSuccessTarget = FastestSuccessTarget,
                    FastestSuccessTransport = FastestSuccessTransport,
                    FastestSuccessMs = FastestSuccessMs,
                    FastestConsensusTarget = FastestConsensusTarget,
                    FastestConsensusTransport = FastestConsensusTransport,
                    FastestConsensusMs = FastestConsensusMs,
                    RecommendationSource = RecommendationSource,
                    RecommendationStatus = RecommendationStatus,
                    RecommendationReason = RecommendationReason
                },
                Results = Array.ConvertAll(Results, result => new ResolverScoreEntry {
                    Target = result.Target,
                    Resolver = result.Resolver,
                    Transport = result.Transport,
                    TotalQueries = 1,
                    SuccessCount = result.Succeeded ? 1 : 0,
                    FailureCount = result.Succeeded ? 0 : 1,
                    SuccessPercent = result.Succeeded ? 100 : 0,
                    AverageMs = result.Succeeded ? result.ElapsedMs : 0,
                    MinMs = result.Succeeded ? result.ElapsedMs : 0,
                    MaxMs = result.Succeeded ? result.ElapsedMs : 0,
                    DistinctAnswerSets = result.Succeeded ? 1 : 0,
                    Rank = result.Rank,
                    IsBest = result.IsFastestSuccess,
                    IsRecommended = result.IsRecommended
                })
            };
        }
    }
}
