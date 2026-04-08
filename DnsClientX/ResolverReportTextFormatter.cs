using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Formats shared resolver reports into human-readable summary strings.
    /// </summary>
    public static class ResolverReportTextFormatter {
        /// <summary>
        /// Describes the fastest successful probe target.
        /// </summary>
        public static string DescribeProbeFastestSuccess(ResolverProbeReportSummary summary, Func<TimeSpan, string> durationFormatter) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            if (string.Equals(summary.FastestSuccessTarget, "none", StringComparison.OrdinalIgnoreCase)) {
                return "none";
            }

            return $"{summary.FastestSuccessTarget} in {durationFormatter(TimeSpan.FromMilliseconds(summary.FastestSuccessMs))} via {summary.FastestSuccessTransport}";
        }

        /// <summary>
        /// Describes the fastest consensus probe target.
        /// </summary>
        public static string DescribeProbeFastestConsensus(ResolverProbeReportSummary summary, Func<TimeSpan, string> durationFormatter) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            if (string.Equals(summary.FastestConsensusTarget, "none", StringComparison.OrdinalIgnoreCase)) {
                return "none";
            }

            return $"{summary.FastestConsensusTarget} in {durationFormatter(TimeSpan.FromMilliseconds(summary.FastestConsensusMs))} via {summary.FastestConsensusTransport}";
        }

        /// <summary>
        /// Describes probe transport coverage.
        /// </summary>
        public static string DescribeProbeTransportCoverage(ResolverProbeReportSummary summary) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            return summary.TransportCoverage.Length == 0
                ? "none"
                : string.Join(" | ", summary.TransportCoverage.Select(entry => $"{entry.Transport} {entry.SuccessfulCount}/{entry.TotalCount}"));
        }

        /// <summary>
        /// Describes probe answer consensus.
        /// </summary>
        public static string DescribeProbeAnswerConsensus(ResolverProbeReportSummary summary) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            return summary.ConsensusTotal == 0
                ? "none"
                : $"{summary.ConsensusCount}/{summary.ConsensusTotal} successful probes agree";
        }

        /// <summary>
        /// Describes mismatched probe targets.
        /// </summary>
        public static string DescribeProbeMismatchedTargets(ResolverProbeReportSummary summary) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            return summary.MismatchedTargets.Length == 0
                ? "none"
                : string.Join(", ", summary.MismatchedTargets);
        }

        /// <summary>
        /// Describes distinct successful probe answer variants.
        /// </summary>
        public static string DescribeProbeAnswerVariants(ResolverProbeReportSummary summary) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            return summary.AnswerVariants.Length == 0
                ? "none"
                : string.Join(" | ", summary.AnswerVariants.Select((variant, index) => $"[{index + 1}] {variant.AnswerSet} <- {string.Join(", ", variant.Targets)}"));
        }

        /// <summary>
        /// Describes the recommended probe target.
        /// </summary>
        public static string DescribeProbeRecommended(ResolverProbeReportSummary summary, Func<TimeSpan, string> durationFormatter) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            if (!summary.RecommendationAvailable) {
                return "none";
            }

            return $"{summary.RecommendedTarget} in {durationFormatter(TimeSpan.FromMilliseconds(summary.RecommendedAverageMs))} via {summary.RecommendedTransport}";
        }

        /// <summary>
        /// Builds the machine-readable probe summary line.
        /// </summary>
        public static string BuildProbeSummaryLine(ResolverProbeReportSummary summary, int exitCode) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            return string.Join(" ", new[] {
                "PROBE_SUMMARY",
                "summary_version=1",
                $"result={(summary.PolicyPassed ? "pass" : "fail")}",
                $"exit_code={exitCode}",
                $"successful={summary.SuccessfulCandidates}",
                $"total={summary.CandidateCount}",
                $"success_percent={summary.SuccessPercent}",
                $"consensus_count={summary.ConsensusCount}",
                $"consensus_total={summary.ConsensusTotal}",
                $"consensus_percent={summary.ConsensusPercent}",
                $"distinct_answer_sets={summary.DistinctAnswerSets}",
                $"fastest_success_target={NormalizeSummaryToken(summary.FastestSuccessTarget)}",
                $"fastest_success_transport={NormalizeSummaryToken(summary.FastestSuccessTransport)}",
                $"fastest_success_ms={(int)Math.Round(summary.FastestSuccessMs, MidpointRounding.AwayFromZero)}",
                $"fastest_consensus_target={NormalizeSummaryToken(summary.FastestConsensusTarget)}",
                $"fastest_consensus_transport={NormalizeSummaryToken(summary.FastestConsensusTransport)}",
                $"fastest_consensus_ms={(int)Math.Round(summary.FastestConsensusMs, MidpointRounding.AwayFromZero)}",
                $"recommended_target={NormalizeSummaryToken(summary.RecommendedTarget)}",
                $"recommended_resolver={NormalizeSummaryToken(summary.RecommendedResolver)}",
                $"recommended_transport={NormalizeSummaryToken(summary.RecommendedTransport)}",
                $"recommended_ms={(int)Math.Round(summary.RecommendedAverageMs, MidpointRounding.AwayFromZero)}",
                $"recommended_status={NormalizeSummaryToken(summary.RecommendationStatus)}",
                $"recommendation_source={NormalizeSummaryToken(summary.RecommendationSource)}",
                $"why_not_recommended={NormalizeSummaryToken(summary.RecommendationReason)}",
                $"policy_reason={NormalizeSummaryToken(summary.PolicyReason)}"
            });
        }

        /// <summary>
        /// Builds the machine-readable benchmark summary line.
        /// </summary>
        public static string BuildBenchmarkSummaryLine(ResolverBenchmarkReportSummary summary, int exitCode) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            return string.Join(" ", new[] {
                "BENCHMARK_SUMMARY",
                "summary_version=1",
                $"result={(summary.PolicyPassed ? "pass" : "fail")}",
                $"exit_code={exitCode}",
                $"candidates={summary.CandidateCount}",
                $"successful_candidates={summary.SuccessfulCandidates}",
                $"total_queries={summary.OverallQueryCount}",
                $"successful_queries={summary.OverallSuccessCount}",
                $"success_percent={summary.OverallSuccessPercent}",
                $"timeout_ms={summary.TimeoutMs}",
                $"concurrency={summary.MaxConcurrency}",
                $"policy_result={(summary.PolicyPassed ? "pass" : "fail")}",
                $"policy_reason={NormalizeSummaryToken(summary.PolicyReason)}",
                $"best_target={NormalizeSummaryToken(summary.RecommendedTarget)}",
                $"best_resolver={NormalizeSummaryToken(summary.RecommendedResolver)}",
                $"best_transport={NormalizeSummaryToken(summary.RecommendedTransport)}",
                $"best_avg_ms={(int)Math.Round(summary.RecommendedAverageMs, MidpointRounding.AwayFromZero)}"
            });
        }

        /// <summary>
        /// Builds the human-readable ranked benchmark line for one result.
        /// </summary>
        public static string BuildBenchmarkRankedLine(ResolverBenchmarkReportResult result, int rankIndex, Func<TimeSpan, string> durationFormatter) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            string average = result.SuccessCount == 0 ? "n/a" : durationFormatter(TimeSpan.FromMilliseconds(result.AverageMs));
            return $"  Ranked {rankIndex}: {result.Target} avg {average}, success {result.SuccessPercent}% ({result.SuccessCount}/{result.TotalQueries}), resolver {result.Resolver}";
        }

        /// <summary>
        /// Describes the best benchmark endpoint.
        /// </summary>
        public static string DescribeBenchmarkBest(IReadOnlyList<ResolverBenchmarkReportResult> results, ResolverBenchmarkReportSummary summary, Func<TimeSpan, string> durationFormatter) {
            if (summary == null) {
                throw new ArgumentNullException(nameof(summary));
            }

            ResolverBenchmarkReportResult? best = summary.RecommendationAvailable
                ? results.FirstOrDefault(result => string.Equals(result.Target, summary.RecommendedTarget, StringComparison.OrdinalIgnoreCase))
                : results.FirstOrDefault(result => result.SuccessCount > 0);

            return best == null
                ? "none"
                : $"{best.Target} in {durationFormatter(TimeSpan.FromMilliseconds(best.AverageMs))} average ({best.SuccessPercent}% success)";
        }

        /// <summary>
        /// Builds the human-readable lines for one probe result block.
        /// </summary>
        public static string[] BuildProbeResultLines(ResolverQueryAttemptResult result, Func<TimeSpan, string> durationFormatter) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            string status = result.Succeeded ? "OK" : "FAIL";
            var lines = new List<string> {
                $"  [{status}] {result.Target} via {result.RequestFormat}",
                $"      Resolver: {result.Resolver}",
                $"      Status: {result.Status}",
                $"      Transport: {result.Transport}",
                $"      Elapsed: {durationFormatter(result.Elapsed)}",
                $"      Answers: {result.AnswerCount}"
            };

            if (!string.Equals(result.EffectiveError, "none", StringComparison.OrdinalIgnoreCase)) {
                lines.Add($"      Error: {result.EffectiveError}");
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Builds the human-readable lines for one probe result block from a shared probe report result.
        /// </summary>
        public static string[] BuildProbeResultLines(ResolverProbeReportResult result, Func<TimeSpan, string> durationFormatter) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            string status = result.Succeeded ? "OK" : "FAIL";
            var lines = new List<string> {
                $"  [{status}] {result.Target} via {result.RequestFormat}",
                $"      Resolver: {result.Resolver}",
                $"      Status: {result.Status}",
                $"      Transport: {result.Transport}",
                $"      Elapsed: {durationFormatter(TimeSpan.FromMilliseconds(result.ElapsedMs))}",
                $"      Answers: {result.AnswerCount}"
            };

            if (!string.Equals(result.Error, "none", StringComparison.OrdinalIgnoreCase)) {
                lines.Add($"      Error: {result.Error}");
            }

            return lines.ToArray();
        }

        /// <summary>
        /// Builds the human-readable lines for one benchmark result block.
        /// </summary>
        public static string[] BuildBenchmarkResultLines(ResolverBenchmarkReportResult result, Func<TimeSpan, string> durationFormatter) {
            if (result == null) {
                throw new ArgumentNullException(nameof(result));
            }

            string status = result.SuccessCount > 0 ? "OK" : "FAIL";
            return new[] {
                $"  [{status}] {result.Target}",
                $"      Resolver: {result.Resolver}",
                $"      Transport: {result.Transport}",
                $"      Success rate: {result.SuccessPercent}% ({result.SuccessCount}/{result.TotalQueries})",
                $"      Average: {durationFormatter(TimeSpan.FromMilliseconds(result.AverageMs))}",
                $"      Min: {durationFormatter(TimeSpan.FromMilliseconds(result.MinMs))}",
                $"      Max: {durationFormatter(TimeSpan.FromMilliseconds(result.MaxMs))}",
                $"      Distinct answer sets: {result.DistinctAnswerSets}"
            };
        }

        private static string NormalizeSummaryToken(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "none";
            }

            string text = value!;
            var chars = new char[text.Length];
            int length = 0;
            bool previousUnderscore = false;

            foreach (char c in text) {
                char normalized;
                if (char.IsLetterOrDigit(c)) {
                    normalized = char.ToLowerInvariant(c);
                    previousUnderscore = false;
                } else {
                    if (previousUnderscore) {
                        continue;
                    }

                    normalized = '_';
                    previousUnderscore = true;
                }

                chars[length++] = normalized;
            }

            if (length == 0) {
                return "none";
            }

            while (length > 0 && chars[length - 1] == '_') {
                length--;
            }

            return length == 0 ? "none" : new string(chars, 0, length);
        }
    }
}
