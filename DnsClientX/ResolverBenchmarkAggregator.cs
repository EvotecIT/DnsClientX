using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Aggregates benchmark attempt observations into a scored benchmark candidate.
    /// </summary>
    public static class ResolverBenchmarkAggregator {
        /// <summary>
        /// Aggregates benchmark attempts for one candidate into a benchmark summary object.
        /// </summary>
        public static ResolverBenchmarkCandidate Aggregate(string displayName, IEnumerable<ResolverBenchmarkAttemptObservation>? attempts) {
            if (string.IsNullOrWhiteSpace(displayName)) {
                throw new ArgumentException("Candidate display name is required.", nameof(displayName));
            }

            ResolverBenchmarkAttemptObservation[] allAttempts = (attempts ?? Array.Empty<ResolverBenchmarkAttemptObservation>())
                .Select(NormalizeAttempt)
                .ToArray();
            ResolverBenchmarkAttemptObservation[] successful = allAttempts
                .Where(result => result.Succeeded)
                .ToArray();
            ResolverBenchmarkAttemptObservation? fastest = successful
                .OrderBy(result => result.ElapsedMs)
                .FirstOrDefault();

            int totalQueries = allAttempts.Length;
            int successCount = successful.Length;
            int failureCount = totalQueries - successCount;
            int successPercent = totalQueries == 0
                ? 0
                : (int)Math.Round((double)successCount * 100 / totalQueries, MidpointRounding.AwayFromZero);

            return new ResolverBenchmarkCandidate {
                Target = displayName,
                Resolver = fastest?.Resolver ?? "none",
                Transport = fastest?.Transport ?? "none",
                TotalQueries = totalQueries,
                SuccessCount = successCount,
                FailureCount = failureCount,
                SuccessPercent = successPercent,
                AverageMs = successCount == 0 ? 0 : Math.Round(successful.Average(result => result.ElapsedMs), 2, MidpointRounding.AwayFromZero),
                MinMs = successCount == 0 ? 0 : Math.Round(successful.Min(result => result.ElapsedMs), 2, MidpointRounding.AwayFromZero),
                MaxMs = successCount == 0 ? 0 : Math.Round(successful.Max(result => result.ElapsedMs), 2, MidpointRounding.AwayFromZero),
                DistinctAnswerSets = successCount == 0 ? 0 : successful.GroupBy(result => result.AnswerSignature, StringComparer.Ordinal).Count()
            };
        }

        private static ResolverBenchmarkAttemptObservation NormalizeAttempt(ResolverBenchmarkAttemptObservation? attempt) {
            return new ResolverBenchmarkAttemptObservation {
                Resolver = string.IsNullOrWhiteSpace(attempt?.Resolver) ? "none" : attempt!.Resolver,
                Transport = string.IsNullOrWhiteSpace(attempt?.Transport) ? "none" : attempt!.Transport,
                ElapsedMs = Math.Round(Math.Max(0, attempt?.ElapsedMs ?? 0), 2, MidpointRounding.AwayFromZero),
                Succeeded = attempt?.Succeeded == true,
                AnswerSignature = string.IsNullOrWhiteSpace(attempt?.AnswerSignature) ? "(no answers)" : attempt!.AnswerSignature
            };
        }
    }
}
