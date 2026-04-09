using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Evaluates aggregated benchmark candidates into ranked results and recommendation data.
    /// </summary>
    public static class ResolverBenchmarkScorer {
        /// <summary>
        /// Evaluates aggregated benchmark candidates under the provided policy.
        /// </summary>
        public static ResolverBenchmarkEvaluation Evaluate(IEnumerable<ResolverBenchmarkCandidate>? candidates, ResolverBenchmarkPolicy? policy = null) {
            ResolverBenchmarkCandidate[] normalized = (candidates ?? Array.Empty<ResolverBenchmarkCandidate>())
                .Select(NormalizeCandidate)
                .ToArray();
            policy ??= new ResolverBenchmarkPolicy();

            ResolverBenchmarkCandidate[] ranked = normalized
                .OrderByDescending(result => result.SuccessPercent)
                .ThenBy(result => result.SuccessCount == 0 ? double.MaxValue : result.AverageMs)
                .ThenBy(result => result.Target, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            ResolverBenchmarkCandidate? best = ranked.FirstOrDefault(result => result.SuccessCount > 0);
            int successfulCandidates = ranked.Count(result => result.SuccessCount > 0);
            int overallQueryCount = ranked.Sum(result => result.TotalQueries);
            int overallSuccessCount = ranked.Sum(result => result.SuccessCount);
            int overallSuccessPercent = overallQueryCount == 0
                ? 0
                : (int)Math.Round((double)overallSuccessCount * 100 / overallQueryCount, MidpointRounding.AwayFromZero);

            (bool passed, string reason) = EvaluatePolicy(ranked, successfulCandidates, overallSuccessPercent, policy);

            return new ResolverBenchmarkEvaluation {
                Results = ranked
                    .Select((result, index) => new ResolverBenchmarkEvaluationEntry {
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
                        Rank = index + 1,
                        IsBest = best != null && string.Equals(result.Target, best.Target, StringComparison.OrdinalIgnoreCase),
                        IsRecommended = passed && best != null && string.Equals(result.Target, best.Target, StringComparison.OrdinalIgnoreCase)
                    })
                    .ToArray(),
                CandidateCount = ranked.Length,
                SuccessfulCandidates = successfulCandidates,
                OverallSuccessCount = overallSuccessCount,
                OverallQueryCount = overallQueryCount,
                OverallSuccessPercent = overallSuccessPercent,
                PolicyPassed = passed,
                PolicyReason = passed ? "none" : reason,
                RecommendationAvailable = passed && best != null,
                RecommendedTarget = best?.Target ?? "none",
                RecommendedResolver = best?.Resolver ?? "none",
                RecommendedTransport = best?.Transport ?? "none",
                RecommendedAverageMs = best?.AverageMs ?? 0
            };
        }

        private static ResolverBenchmarkCandidate NormalizeCandidate(ResolverBenchmarkCandidate? candidate) {
            int totalQueries = Math.Max(0, candidate?.TotalQueries ?? 0);
            int successCount = Math.Max(0, candidate?.SuccessCount ?? 0);
            int failureCount = Math.Max(0, candidate?.FailureCount ?? 0);
            if (successCount + failureCount != totalQueries) {
                totalQueries = successCount + failureCount;
            }

            int successPercent = totalQueries == 0
                ? 0
                : (int)Math.Round((double)successCount * 100 / totalQueries, MidpointRounding.AwayFromZero);

            return new ResolverBenchmarkCandidate {
                Target = candidate?.Target?.Trim() ?? string.Empty,
                Resolver = string.IsNullOrWhiteSpace(candidate?.Resolver) ? "none" : candidate!.Resolver,
                Transport = string.IsNullOrWhiteSpace(candidate?.Transport) ? "none" : candidate!.Transport,
                TotalQueries = totalQueries,
                SuccessCount = successCount,
                FailureCount = failureCount,
                SuccessPercent = successPercent,
                AverageMs = Math.Round(Math.Max(0, candidate?.AverageMs ?? 0), 2, MidpointRounding.AwayFromZero),
                MinMs = Math.Round(Math.Max(0, candidate?.MinMs ?? 0), 2, MidpointRounding.AwayFromZero),
                MaxMs = Math.Round(Math.Max(0, candidate?.MaxMs ?? 0), 2, MidpointRounding.AwayFromZero),
                DistinctAnswerSets = Math.Max(0, candidate?.DistinctAnswerSets ?? 0)
            };
        }

        private static (bool Passed, string Reason) EvaluatePolicy(
            IReadOnlyList<ResolverBenchmarkCandidate> ranked,
            int successfulCandidates,
            int overallSuccessPercent,
            ResolverBenchmarkPolicy policy) {
            if (successfulCandidates == 0) {
                return (false, "no successful candidates");
            }

            if (policy.MinSuccessfulCandidates.HasValue && successfulCandidates < policy.MinSuccessfulCandidates.Value) {
                return (false, $"successful candidates {successfulCandidates}/{ranked.Count} below required count {policy.MinSuccessfulCandidates.Value}");
            }

            if (policy.MinSuccessPercent.HasValue && overallSuccessPercent < policy.MinSuccessPercent.Value) {
                return (false, $"success rate {overallSuccessPercent}% below required {policy.MinSuccessPercent.Value}%");
            }

            return (true, "none");
        }
    }
}
