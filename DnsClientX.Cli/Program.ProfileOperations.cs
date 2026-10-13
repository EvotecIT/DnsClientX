using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static partial class Program {
        private static bool ValidateOperationalMode(CliOptions options, out string? error) {
            error = null;
            bool profile = options.ProfilePath != null;
            bool comparison = options.CompareBaselinePath != null;
            if (!profile && !comparison) {
                if (options.ProfileSavePath != null || options.ReferenceResponsePath != null) { error = "--profile-save requires --profile; --reference-response requires --resolver-compare."; return false; }
                return true;
            }
            var allowed = new HashSet<string>(profile
                ? new[] { "--profile", "--profile-save", "--benchmark", "--format", "--dnssec-verifier" }
                : new[] { "--resolver-compare", "--reference-response", "--format" }, StringComparer.OrdinalIgnoreCase);
            if (profile && comparison || options.Domain != null || options.OutputFormat == QueryOutputFormat.Raw || options.ProvidedSwitches.Any(s => !allowed.Contains(s))) {
                error = "Profile/comparison modes support pretty or JSON output; profile run settings come from the profile. Use --profile path [--benchmark] [--profile-save report.json], or --resolver-compare baseline.json current.json [--reference-response references.json].";
                return false;
            }
            if (string.IsNullOrWhiteSpace(options.ProfilePath) && profile || comparison && (string.IsNullOrWhiteSpace(options.CompareBaselinePath) || string.IsNullOrWhiteSpace(options.CompareCurrentPath))) { error = "A nonblank profile or snapshot path is required."; return false; }
            return true;
        }

        private static async Task<int> RunProfileAsync(CliOptions options, CancellationToken token) {
            ResolverHealthProfile profile = ResolverHealthProfileStore.Load(options.ProfilePath!);
            IDnsSecSignatureVerifier? verifier = string.IsNullOrWhiteSpace(options.DnsSecVerifierPath) ? null : DnsSecSignatureVerifierLoader.Load(options.DnsSecVerifierPath!);
            ResolverHealthReport report = await ResolverHealthWorkflow.RunAsync(profile, options.Benchmark, verifier, CreateBuiltInOverride(), CreateExplicitOverride(), token).ConfigureAwait(false);
            string json = DnsClientXJsonSerializer.Serialize(report);
            if (options.ProfileSavePath != null) {
                File.WriteAllText(Path.GetFullPath(options.ProfileSavePath), json);
            }
            if (options.OutputFormat == QueryOutputFormat.Json) {
                Console.WriteLine(json);
            } else {
                WriteHumanLine($"Health profile: {report.ProfileName}");
                foreach (ResolverProbeReport query in report.Queries) {
                    WriteHumanLine($"  {query.Summary.Name} {query.Summary.RecordType}: {query.Summary.SuccessfulCandidates}/{query.Summary.CandidateCount} successes; policy {(query.Summary.PolicyPassed ? "pass" : "fail")}");
                    foreach (ResolverProbeReportResult result in query.Results) {
                        WriteProbeResult(result);
                    }
                }
                if (report.Benchmark != null) {
                    foreach (ResolverBenchmarkReportResult result in report.Benchmark.Results) {
                        WriteBenchmarkResult(result);
                    }
                }
                WriteHumanLine($"Policy result: {(report.PolicyPassed ? "pass" : "fail")}");
                WriteHumanLine(report.AnswerVariationNote);
            }
            return report.PolicyPassed ? 0 : 3;
        }

        private static int RunSnapshotComparison(CliOptions options) {
            ResolverScoreSnapshot baseline = ResolverScoreStore.Load(options.CompareBaselinePath!);
            ResolverScoreSnapshot current = ResolverScoreStore.Load(options.CompareCurrentPath!);
            ResolverAnswerReference[]? references = options.ReferenceResponsePath == null ? null :
                DnsClientXJsonSerializer.Deserialize<ResolverAnswerReference[]>(File.ReadAllText(Path.GetFullPath(options.ReferenceResponsePath)));
            ResolverScoreComparison comparison = ResolverScoreComparer.Compare(baseline, current, references);
            if (options.OutputFormat == QueryOutputFormat.Json) {
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(comparison));
            } else {
                WriteHumanLine($"Comparable scope: {comparison.ScopeComparable}; health regression: {comparison.HealthRegression}");
                foreach (string reason in comparison.IncomparableReasons) {
                    WriteHumanLine($"  {reason}");
                }
                foreach (ResolverScoreChange change in comparison.Candidates) {
                    WriteHumanLine($"  {change.Target}: {change.Change}; success delta {change.SuccessPercentDelta?.ToString() ?? "unavailable"} percentage points; latency delta {change.AverageMsDelta?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable"} ms");
                }
                foreach (ResolverAnswerChange answer in comparison.Answers) {
                    WriteHumanLine($"  {answer.Target} {answer.Name} {answer.RecordType}: answers {(!answer.Changed.HasValue ? "unavailable" : answer.Changed.Value ? "changed" : "unchanged")}; reference {answer.ReferenceComparison}");
                }
                WriteHumanLine(comparison.AnswerVariationNote);
            }
            return comparison.HealthRegression ? 3 : 0;
        }
    }
}
