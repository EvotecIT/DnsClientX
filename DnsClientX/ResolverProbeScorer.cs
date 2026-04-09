using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Evaluates probe observations into ranked results, policy status, and recommendation data.
    /// </summary>
    public static class ResolverProbeScorer {
        /// <summary>
        /// Evaluates resolver probe observations under the provided policy.
        /// </summary>
        public static ResolverProbeEvaluation Evaluate(IEnumerable<ResolverProbeObservation>? observations, ResolverProbePolicy? policy = null) {
            ResolverProbeObservation[] allResults = (observations ?? Array.Empty<ResolverProbeObservation>())
                .Select(NormalizeObservation)
                .ToArray();
            policy ??= new ResolverProbePolicy();

            ResolverProbeObservation[] successful = allResults
                .Where(result => result.Succeeded)
                .ToArray();
            string[][] groups = GetAnswerGroups(successful);
            ResolverProbeObservation? fastestSuccess = successful
                .OrderBy(result => result.ElapsedMs)
                .FirstOrDefault();
            ResolverProbeObservation? fastestConsensus = groups.Length == 0
                ? null
                : successful
                    .Where(result => string.Equals(result.AnswerSignature, groups[0][0], StringComparison.Ordinal))
                    .OrderBy(result => result.ElapsedMs)
                    .FirstOrDefault();

            (bool passed, string reason) = EvaluatePolicy(allResults, successful, groups, policy);
            ResolverProbeObservation? recommended = GetRecommended(successful, groups, passed, policy);
            int successPercent = allResults.Length == 0
                ? 0
                : (int)Math.Round((double)successful.Length * 100 / allResults.Length, MidpointRounding.AwayFromZero);
            int consensusCount = groups.Length == 0 ? 0 : groups[0].Length;
            int consensusPercent = successful.Length == 0
                ? 0
                : (int)Math.Round((double)consensusCount * 100 / successful.Length, MidpointRounding.AwayFromZero);

            Dictionary<string, int> answerGroupSizes = successful
                .GroupBy(result => result.AnswerSignature, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            ResolverProbeObservation[] ranked = allResults
                .OrderByDescending(result => result.Succeeded)
                .ThenByDescending(result => result.Succeeded ? answerGroupSizes[result.AnswerSignature] : 0)
                .ThenBy(result => result.Succeeded ? result.ElapsedMs : double.MaxValue)
                .ThenBy(result => result.Target, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new ResolverProbeEvaluation {
                Results = ranked
                    .Select((result, index) => new ResolverProbeEvaluationEntry {
                        Target = result.Target,
                        Resolver = result.Resolver,
                        Transport = result.Transport,
                        ElapsedMs = result.ElapsedMs,
                        Succeeded = result.Succeeded,
                        Rank = index + 1,
                        IsFastestSuccess = IsSameTarget(result, fastestSuccess),
                        IsFastestConsensus = IsSameTarget(result, fastestConsensus),
                        IsRecommended = IsSameTarget(result, recommended)
                    })
                    .ToArray(),
                CandidateCount = allResults.Length,
                SuccessfulCandidates = successful.Length,
                SuccessPercent = successPercent,
                PolicyPassed = passed,
                PolicyReason = passed ? "none" : reason,
                DistinctAnswerSets = groups.Length,
                ConsensusCount = consensusCount,
                ConsensusTotal = successful.Length,
                ConsensusPercent = consensusPercent,
                FastestSuccessTarget = fastestSuccess?.Target ?? "none",
                FastestSuccessResolver = fastestSuccess?.Resolver ?? "none",
                FastestSuccessTransport = fastestSuccess?.Transport ?? "none",
                FastestSuccessMs = Math.Round(fastestSuccess?.ElapsedMs ?? 0, 2, MidpointRounding.AwayFromZero),
                FastestConsensusTarget = fastestConsensus?.Target ?? "none",
                FastestConsensusResolver = fastestConsensus?.Resolver ?? "none",
                FastestConsensusTransport = fastestConsensus?.Transport ?? "none",
                FastestConsensusMs = Math.Round(fastestConsensus?.ElapsedMs ?? 0, 2, MidpointRounding.AwayFromZero),
                RecommendationAvailable = recommended != null,
                RecommendedTarget = recommended?.Target ?? "none",
                RecommendedResolver = recommended?.Resolver ?? "none",
                RecommendedTransport = recommended?.Transport ?? "none",
                RecommendedAverageMs = Math.Round(recommended?.ElapsedMs ?? 0, 2, MidpointRounding.AwayFromZero),
                RecommendationSource = GetRecommendationSource(successful, groups, policy, recommended),
                RecommendationStatus = GetRecommendationStatus(passed, recommended),
                RecommendationReason = GetRecommendationReason(successful, groups, passed, policy, recommended)
            };
        }

        private static ResolverProbeObservation NormalizeObservation(ResolverProbeObservation? observation) {
            return new ResolverProbeObservation {
                Target = observation?.Target?.Trim() ?? string.Empty,
                Resolver = string.IsNullOrWhiteSpace(observation?.Resolver) ? "none" : observation!.Resolver,
                Transport = string.IsNullOrWhiteSpace(observation?.Transport) ? "none" : observation!.Transport,
                ElapsedMs = Math.Round(Math.Max(0, observation?.ElapsedMs ?? 0), 2, MidpointRounding.AwayFromZero),
                Succeeded = observation?.Succeeded == true,
                AnswerSignature = string.IsNullOrWhiteSpace(observation?.AnswerSignature) ? "(no answers)" : observation!.AnswerSignature
            };
        }

        private static (bool Passed, string Reason) EvaluatePolicy(
            IReadOnlyList<ResolverProbeObservation> allResults,
            IReadOnlyList<ResolverProbeObservation> successful,
            IReadOnlyList<string[]> groups,
            ResolverProbePolicy policy) {
            if (successful.Count == 0) {
                return (false, "no successful probes");
            }

            if (policy.MinSuccessCount.HasValue && successful.Count < policy.MinSuccessCount.Value) {
                return (false, $"successful probes {successful.Count}/{allResults.Count} below required count {policy.MinSuccessCount.Value}");
            }

            int successPercent = allResults.Count == 0
                ? 0
                : (int)Math.Round((double)successful.Count * 100 / allResults.Count, MidpointRounding.AwayFromZero);
            if (policy.MinSuccessPercent.HasValue && successPercent < policy.MinSuccessPercent.Value) {
                return (false, $"success rate {successPercent}% below required {policy.MinSuccessPercent.Value}%");
            }

            int consensusPercent = successful.Count == 0
                ? 0
                : (int)Math.Round((double)groups[0].Length * 100 / successful.Count, MidpointRounding.AwayFromZero);
            if (policy.RequireConsensus && groups.Count > 1) {
                return (false, $"consensus required but top answer set reached {consensusPercent}%");
            }

            if (policy.MinConsensusPercent.HasValue && consensusPercent < policy.MinConsensusPercent.Value) {
                return (false, $"consensus {consensusPercent}% below required {policy.MinConsensusPercent.Value}%");
            }

            return (true, "none");
        }

        private static ResolverProbeObservation? GetRecommended(
            IReadOnlyList<ResolverProbeObservation> successful,
            IReadOnlyList<string[]> groups,
            bool policyPassed,
            ResolverProbePolicy policy) {
            if (!policyPassed || successful.Count == 0 || groups.Count == 0) {
                return null;
            }

            string leadingSignature = groups[0][0];
            if (groups.Count == 1) {
                return successful
                    .Where(result => string.Equals(result.AnswerSignature, leadingSignature, StringComparison.Ordinal))
                    .OrderBy(result => result.ElapsedMs)
                    .FirstOrDefault();
            }

            if (groups[0].Length == groups[1].Length) {
                return null;
            }

            bool hasConsensusPolicy = policy.RequireConsensus || policy.MinConsensusPercent.HasValue;
            if (!hasConsensusPolicy) {
                return null;
            }

            return successful
                .Where(result => string.Equals(result.AnswerSignature, leadingSignature, StringComparison.Ordinal))
                .OrderBy(result => result.ElapsedMs)
                .FirstOrDefault();
        }

        private static string GetRecommendationReason(
            IReadOnlyList<ResolverProbeObservation> successful,
            IReadOnlyList<string[]> groups,
            bool policyPassed,
            ResolverProbePolicy policy,
            ResolverProbeObservation? recommended) {
            if (recommended != null) {
                return "none";
            }

            if (!policyPassed) {
                return "policy failed";
            }

            if (successful.Count == 0) {
                return "no successful probes";
            }

            if (groups.Count == 0) {
                return "no answer groups";
            }

            if (groups.Count == 1) {
                return "none";
            }

            if (groups[0].Length == groups[1].Length) {
                return "top answer sets tied";
            }

            bool hasConsensusPolicy = policy.RequireConsensus || policy.MinConsensusPercent.HasValue;
            return hasConsensusPolicy ? "none" : "consensus policy not enabled";
        }

        private static string GetRecommendationSource(
            IReadOnlyList<ResolverProbeObservation> successful,
            IReadOnlyList<string[]> groups,
            ResolverProbePolicy policy,
            ResolverProbeObservation? recommended) {
            if (recommended == null) {
                return "none";
            }

            if (successful.Count <= 1) {
                return "single success fallback";
            }

            if (groups.Count <= 1) {
                return "unanimous agreement";
            }

            bool hasConsensusPolicy = policy.RequireConsensus || policy.MinConsensusPercent.HasValue;
            return hasConsensusPolicy ? "consensus policy majority" : "none";
        }

        private static string GetRecommendationStatus(bool policyPassed, ResolverProbeObservation? recommended) {
            if (recommended != null) {
                return "selected";
            }

            return policyPassed ? "unavailable" : "blocked by policy";
        }

        private static string[][] GetAnswerGroups(IEnumerable<ResolverProbeObservation> successfulResults) {
            return successfulResults
                .GroupBy(result => result.AnswerSignature, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => Enumerable.Repeat(group.Key, group.Count()).ToArray())
                .ToArray();
        }

        private static bool IsSameTarget(ResolverProbeObservation result, ResolverProbeObservation? other) {
            return other != null && string.Equals(result.Target, other.Target, StringComparison.OrdinalIgnoreCase);
        }
    }
}
