using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Builds shared probe reports from executed query attempts.
    /// </summary>
    public static class ResolverProbeReportBuilder {
        /// <summary>
        /// Builds a shared probe report from executed attempts and policy settings.
        /// </summary>
        public static ResolverProbeReport Build(
            IReadOnlyList<ResolverQueryAttemptResult> attempts,
            string name,
            DnsRecordType recordType,
            int timeoutMs,
            ResolverProbePolicy policy) {
            if (attempts == null) {
                throw new ArgumentNullException(nameof(attempts));
            }

            policy ??= new ResolverProbePolicy();
            ResolverProbeObservation[] observations = attempts
                .Select(result => new ResolverProbeObservation {
                    Target = result.Target,
                    Resolver = result.Resolver,
                    Transport = result.Transport,
                    ElapsedMs = Math.Round(result.Elapsed.TotalMilliseconds, 2, MidpointRounding.AwayFromZero),
                    Succeeded = result.Succeeded,
                    AnswerSignature = result.AnswerSignature
                })
                .ToArray();

            ResolverProbeEvaluation evaluation = ResolverProbeScorer.Evaluate(observations, policy);
            Dictionary<string, ResolverQueryAttemptResult> attemptMap = attempts.ToDictionary(result => result.Target, StringComparer.OrdinalIgnoreCase);

            ResolverProbeReportResult[] results = evaluation.Results
                .Select(result => {
                    ResolverQueryAttemptResult attempt = attemptMap[result.Target];
                    return new ResolverProbeReportResult {
                        Target = result.Target,
                        Resolver = attempt.Resolver,
                        RequestFormat = attempt.RequestFormat,
                        Transport = attempt.Transport,
                        Status = attempt.Status,
                        Error = attempt.EffectiveError,
                        ElapsedMs = result.ElapsedMs,
                        AnswerCount = attempt.AnswerCount,
                        Succeeded = result.Succeeded,
                        Rank = result.Rank,
                        IsFastestSuccess = result.IsFastestSuccess,
                        IsFastestConsensus = result.IsFastestConsensus,
                        IsRecommended = result.IsRecommended,
                        PolicyPassed = evaluation.PolicyPassed,
                        PolicyReason = evaluation.PolicyReason
                    };
                })
                .ToArray();

            ResolverQueryAttemptResult[] successful = attempts.Where(result => result.Succeeded).ToArray();
            ResolverQueryAttemptResult[][] groups = successful
                .GroupBy(result => result.AnswerSignature, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.ToArray())
                .ToArray();

            ResolverProbeReportSummary summary = new ResolverProbeReportSummary {
                Name = name ?? string.Empty,
                RecordType = recordType,
                TimeoutMs = timeoutMs,
                CandidateCount = evaluation.CandidateCount,
                SuccessfulCandidates = evaluation.SuccessfulCandidates,
                FailedCandidates = evaluation.CandidateCount - evaluation.SuccessfulCandidates,
                SuccessPercent = evaluation.SuccessPercent,
                PolicyPassed = evaluation.PolicyPassed,
                PolicyReason = evaluation.PolicyReason,
                RequiredMinSuccessCount = policy.MinSuccessCount,
                RequiredMinSuccessPercent = policy.MinSuccessPercent,
                RequiredMinConsensusPercent = policy.MinConsensusPercent,
                RequireConsensus = policy.RequireConsensus,
                DistinctAnswerSets = evaluation.DistinctAnswerSets,
                ConsensusCount = evaluation.ConsensusCount,
                ConsensusTotal = evaluation.ConsensusTotal,
                ConsensusPercent = evaluation.ConsensusPercent,
                FastestSuccessTarget = evaluation.FastestSuccessTarget,
                FastestSuccessResolver = evaluation.FastestSuccessResolver,
                FastestSuccessTransport = evaluation.FastestSuccessTransport,
                FastestSuccessMs = evaluation.FastestSuccessMs,
                FastestConsensusTarget = evaluation.FastestConsensusTarget,
                FastestConsensusResolver = evaluation.FastestConsensusResolver,
                FastestConsensusTransport = evaluation.FastestConsensusTransport,
                FastestConsensusMs = evaluation.FastestConsensusMs,
                RecommendationAvailable = evaluation.RecommendationAvailable,
                RecommendedTarget = evaluation.RecommendedTarget,
                RecommendedResolver = evaluation.RecommendedResolver,
                RecommendedTransport = evaluation.RecommendedTransport,
                RecommendedAverageMs = evaluation.RecommendedAverageMs,
                RecommendationSource = evaluation.RecommendationSource,
                RecommendationStatus = evaluation.RecommendationStatus,
                RecommendationReason = evaluation.RecommendationReason,
                TransportCoverage = attempts
                    .GroupBy(result => result.Transport, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new ResolverProbeTransportCoverage {
                        Transport = group.Key,
                        SuccessfulCount = group.Count(result => result.Succeeded),
                        TotalCount = group.Count()
                    })
                    .ToArray(),
                MismatchedTargets = groups.Length <= 1
                    ? Array.Empty<string>()
                    : groups
                        .Skip(1)
                        .SelectMany(group => group)
                        .Select(result => result.Target)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                AnswerVariants = groups
                    .Select(group => new ResolverProbeAnswerVariant {
                        AnswerSet = DescribeAnswerSet(group[0]),
                        Targets = group
                            .Select(result => result.Target)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    })
                    .ToArray()
            };

            return new ResolverProbeReport {
                Results = results,
                Summary = summary,
                Evaluation = evaluation,
                Snapshot = evaluation.CreateSnapshot(policy, new[] { name ?? string.Empty }, new[] { recordType }, timeoutMs)
            };
        }

        private static string DescribeAnswerSet(ResolverQueryAttemptResult attempt) {
            DnsResponse? response = attempt.Response;
            if (response?.Answers == null || response.Answers.Length == 0) {
                return "(no answers)";
            }

            string[] values = response.Answers
                .Select(answer => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", answer.Name, answer.Type, answer.Data))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return string.Join("; ", values);
        }
    }
}
