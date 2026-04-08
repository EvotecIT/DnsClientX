using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Builds shared benchmark reports from executed query attempts.
    /// </summary>
    public static class ResolverBenchmarkReportBuilder {
        /// <summary>
        /// Builds a shared benchmark report from executed attempts and policy settings.
        /// </summary>
        public static ResolverBenchmarkReport Build(
            IReadOnlyList<ResolverQueryAttemptResult> attempts,
            string[] names,
            DnsRecordType[] recordTypes,
            int attemptsPerCombination,
            int maxConcurrency,
            int timeoutMs,
            ResolverBenchmarkPolicy policy) {
            if (attempts == null) {
                throw new ArgumentNullException(nameof(attempts));
            }

            policy ??= new ResolverBenchmarkPolicy();
            ResolverBenchmarkCandidate[] candidates = attempts
                .GroupBy(result => result.Target, StringComparer.OrdinalIgnoreCase)
                .Select(group => ResolverBenchmarkAggregator.Aggregate(
                    group.Key,
                    group.Select(result => new ResolverBenchmarkAttemptObservation {
                        Resolver = result.Resolver,
                        Transport = result.Transport,
                        ElapsedMs = Math.Round(result.Elapsed.TotalMilliseconds, 2, MidpointRounding.AwayFromZero),
                        Succeeded = result.Succeeded,
                        AnswerSignature = result.AnswerSignature
                    })))
                .ToArray();

            return Build(
                candidates,
                names,
                recordTypes,
                attemptsPerCombination,
                maxConcurrency,
                timeoutMs,
                policy);
        }

        /// <summary>
        /// Builds a shared benchmark report from aggregated candidate summaries and policy settings.
        /// </summary>
        public static ResolverBenchmarkReport Build(
            IReadOnlyList<ResolverBenchmarkCandidate> candidates,
            string[] names,
            DnsRecordType[] recordTypes,
            int attemptsPerCombination,
            int maxConcurrency,
            int timeoutMs,
            ResolverBenchmarkPolicy policy) {
            if (candidates == null) {
                throw new ArgumentNullException(nameof(candidates));
            }

            policy ??= new ResolverBenchmarkPolicy();

            ResolverBenchmarkEvaluation evaluation = ResolverBenchmarkScorer.Evaluate(candidates, policy);
            Dictionary<string, ResolverBenchmarkCandidate> candidateMap = candidates.ToDictionary(candidate => candidate.Target, StringComparer.OrdinalIgnoreCase);

            ResolverBenchmarkReportResult[] results = evaluation.Results
                .Select(entry => {
                    ResolverBenchmarkCandidate candidate = candidateMap[entry.Target];
                    return new ResolverBenchmarkReportResult {
                        Target = candidate.Target,
                        Resolver = candidate.Resolver,
                        Transport = candidate.Transport,
                        Domains = names ?? Array.Empty<string>(),
                        RecordTypes = recordTypes ?? Array.Empty<DnsRecordType>(),
                        AttemptsPerCombination = attemptsPerCombination,
                        MaxConcurrency = maxConcurrency,
                        TimeoutMs = timeoutMs,
                        TotalQueries = candidate.TotalQueries,
                        SuccessCount = candidate.SuccessCount,
                        FailureCount = candidate.FailureCount,
                        SuccessPercent = candidate.SuccessPercent,
                        AverageMs = candidate.AverageMs,
                        MinMs = candidate.MinMs,
                        MaxMs = candidate.MaxMs,
                        DistinctAnswerSets = candidate.DistinctAnswerSets,
                        Rank = entry.Rank,
                        IsBest = entry.IsBest,
                        IsRecommended = entry.IsRecommended,
                        PolicyPassed = evaluation.PolicyPassed,
                        PolicyReason = evaluation.PolicyReason,
                        RequiredMinSuccessPercent = policy.MinSuccessPercent,
                        RequiredMinSuccessfulCandidates = policy.MinSuccessfulCandidates,
                        CandidateCount = evaluation.CandidateCount,
                        SuccessfulCandidates = evaluation.SuccessfulCandidates,
                        OverallSuccessPercent = evaluation.OverallSuccessPercent,
                        OverallSuccessCount = evaluation.OverallSuccessCount,
                        OverallQueryCount = evaluation.OverallQueryCount
                    };
                })
                .ToArray();

            return new ResolverBenchmarkReport {
                Results = results,
                Summary = new ResolverBenchmarkReportSummary {
                    Domains = names ?? Array.Empty<string>(),
                    RecordTypes = recordTypes ?? Array.Empty<DnsRecordType>(),
                    AttemptsPerCombination = attemptsPerCombination,
                    MaxConcurrency = maxConcurrency,
                    TimeoutMs = timeoutMs,
                    CandidateCount = evaluation.CandidateCount,
                    SuccessfulCandidates = evaluation.SuccessfulCandidates,
                    OverallSuccessCount = evaluation.OverallSuccessCount,
                    OverallQueryCount = evaluation.OverallQueryCount,
                    OverallSuccessPercent = evaluation.OverallSuccessPercent,
                    PolicyPassed = evaluation.PolicyPassed,
                    PolicyReason = evaluation.PolicyReason,
                    RequiredMinSuccessPercent = policy.MinSuccessPercent,
                    RequiredMinSuccessfulCandidates = policy.MinSuccessfulCandidates,
                    RecommendedTarget = evaluation.RecommendedTarget,
                    RecommendedResolver = evaluation.RecommendedResolver,
                    RecommendedTransport = evaluation.RecommendedTransport,
                    RecommendedAverageMs = evaluation.RecommendedAverageMs,
                    RecommendationAvailable = evaluation.RecommendationAvailable
                },
                Evaluation = evaluation,
                Snapshot = evaluation.CreateSnapshot(policy, names ?? Array.Empty<string>(), recordTypes ?? Array.Empty<DnsRecordType>(), attemptsPerCombination, maxConcurrency, timeoutMs)
            };
        }
    }
}
