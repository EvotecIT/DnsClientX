using System;

namespace DnsClientX {
    /// <summary>
    /// Describes the ranked outcome of a benchmark evaluation.
    /// </summary>
    public sealed class ResolverBenchmarkEvaluation {
        /// <summary>
        /// Gets or sets the ranked benchmark entries.
        /// </summary>
        public ResolverBenchmarkEvaluationEntry[] Results { get; set; } = Array.Empty<ResolverBenchmarkEvaluationEntry>();

        /// <summary>
        /// Gets or sets the total candidate count in the evaluation.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Gets or sets the number of successful candidates in the evaluation.
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
        /// Gets or sets a value indicating whether the evaluation policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Gets or sets the policy failure reason, or <c>none</c> when the policy passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";

        /// <summary>
        /// Gets or sets a value indicating whether a recommendation is available.
        /// </summary>
        public bool RecommendationAvailable { get; set; }

        /// <summary>
        /// Gets or sets the recommended candidate label.
        /// </summary>
        public string RecommendedTarget { get; set; } = "none";

        /// <summary>
        /// Gets or sets the resolver observed on the recommended candidate.
        /// </summary>
        public string RecommendedResolver { get; set; } = "none";

        /// <summary>
        /// Gets or sets the transport observed on the recommended candidate.
        /// </summary>
        public string RecommendedTransport { get; set; } = "none";

        /// <summary>
        /// Gets or sets the average latency of the recommended candidate in milliseconds.
        /// </summary>
        public double RecommendedAverageMs { get; set; }

        /// <summary>
        /// Creates a persisted resolver score snapshot from the evaluation.
        /// </summary>
        public ResolverScoreSnapshot CreateSnapshot(
            ResolverBenchmarkPolicy policy,
            string[] domains,
            DnsRecordType[] recordTypes,
            int attemptsPerCombination,
            int maxConcurrency,
            int timeoutMs) {
            return new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Benchmark,
                    Domains = domains ?? Array.Empty<string>(),
                    RecordTypes = recordTypes ?? Array.Empty<DnsRecordType>(),
                    AttemptsPerCombination = attemptsPerCombination,
                    MaxConcurrency = maxConcurrency,
                    TimeoutMs = timeoutMs,
                    CandidateCount = CandidateCount,
                    SuccessfulCandidates = SuccessfulCandidates,
                    OverallSuccessCount = OverallSuccessCount,
                    OverallQueryCount = OverallQueryCount,
                    OverallSuccessPercent = OverallSuccessPercent,
                    PolicyPassed = PolicyPassed,
                    PolicyReason = PolicyReason,
                    RequiredMinSuccessPercent = policy?.MinSuccessPercent,
                    RequiredMinSuccessfulCandidates = policy?.MinSuccessfulCandidates,
                    RecommendationAvailable = RecommendationAvailable,
                    RecommendedTarget = RecommendedTarget,
                    RecommendedResolver = RecommendedResolver,
                    RecommendedTransport = RecommendedTransport,
                    RecommendedAverageMs = RecommendedAverageMs,
                    RecommendationSource = RecommendationAvailable ? "policy_pass_best_success" : "none",
                    RecommendationStatus = RecommendationAvailable ? "selected" : (PolicyPassed ? "unavailable" : "blocked by policy"),
                    RecommendationReason = RecommendationAvailable ? "none" : (PolicyPassed ? "no successful candidates" : "policy failed")
                },
                Results = Array.ConvertAll(Results, result => new ResolverScoreEntry {
                    Target = result.Target,
                    Resolver = result.Resolver,
                    Transport = result.Transport,
                    TotalQueries = result.TotalQueries,
                    SuccessCount = result.SuccessCount,
                    FailureCount = result.FailureCount,
                    SuccessPercent = result.SuccessPercent,
                    AverageMs = result.AverageMs,
                    MinMs = result.MinMs,
                    MaxMs = result.MaxMs,
                    DistinctAnswerSets = result.DistinctAnswerSets,
                    Rank = result.Rank,
                    IsBest = result.IsBest,
                    IsRecommended = result.IsRecommended
                })
            };
        }
    }
}
