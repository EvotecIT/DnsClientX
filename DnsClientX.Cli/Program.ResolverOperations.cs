using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static partial class Program {
        private static async Task<int> RunBenchmarkAsync(CliOptions options, CancellationToken cancellationToken) {
            string[] domains = options.BenchmarkDomains
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            DnsRecordType[] recordTypes = options.BenchmarkRecordTypes
                .Distinct()
                .ToArray();
            ResolverExecutionTargetSource targetSource = CreateBenchmarkTargetSource(options);
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, cancellationToken).ConfigureAwait(false);
            if (options.HasCustomEndpointInputs && targets.Length == 0) {
                Console.Error.WriteLine("No custom resolver endpoints were supplied.");
                return 1;
            }

            return await RunBenchmarkWithTargetsAsync(
                options,
                targets,
                domains,
                recordTypes,
                GetBenchmarkEndpointProfile(options),
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> RunBenchmarkWithTargetsAsync(
            CliOptions options,
            ResolverExecutionTarget[] targets,
            string[] domains,
            DnsRecordType[] recordTypes,
            string? endpointProfile,
            CancellationToken cancellationToken) {
            if (options.OutputFormat == QueryOutputFormat.Json) {
                ResolverBenchmarkReport jsonReport = await ExecuteBenchmarkReportAsync(targets, domains, recordTypes, options, cancellationToken).ConfigureAwait(false);
                if (options.BenchmarkSavePath != null) {
                    SaveResolverScoreSnapshot(options.BenchmarkSavePath, jsonReport.Snapshot);
                }
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(jsonReport));
                return GetBenchmarkExitCode(jsonReport.Evaluation, options);
            }
            Console.WriteLine("Benchmark:");
            WriteHumanLine($"  Domains: {string.Join(", ", domains)}");
            Console.WriteLine($"  Types: {string.Join(", ", recordTypes)}");
            Console.WriteLine($"  Attempts per combination: {options.BenchmarkAttempts}");
            Console.WriteLine($"  Timeout (ms): {options.BenchmarkTimeoutMs}");
            Console.WriteLine($"  Concurrency: {options.BenchmarkConcurrency}");
            Console.WriteLine($"  Connections: {options.BenchmarkConnectionMode}");
            Console.WriteLine($"  Detail mode: {(options.BenchmarkSummaryOnly ? "summary-only" : "full")}");
            if (!string.IsNullOrWhiteSpace(endpointProfile)) {
                WriteHumanLine($"  Endpoint profile: {endpointProfile}");
            }

            Console.WriteLine($"  Candidates: {targets.Length}");
            Console.WriteLine($"  Queries per candidate: {domains.Length * recordTypes.Length * options.BenchmarkAttempts}");

            ResolverBenchmarkReport report = await ExecuteBenchmarkReportAsync(
                targets,
                domains,
                recordTypes,
                options,
                cancellationToken).ConfigureAwait(false);
            if (!options.BenchmarkSummaryOnly) {
                foreach (ResolverBenchmarkReportResult result in report.Results) {
                    WriteBenchmarkResult(result);
                }
            }

            return WriteBenchmarkSummary(report, options);
        }

        private static void WriteBenchmarkResult(ResolverBenchmarkReportResult result) {
            foreach (string line in ResolverReportTextFormatter.BuildBenchmarkResultLines(result, FormatDuration)) {
                Console.WriteLine(line);
            }
        }

        private static int WriteBenchmarkSummary(ResolverBenchmarkReport report, CliOptions options) {
            ResolverBenchmarkEvaluation evaluation = report.Evaluation;

            Console.WriteLine("Benchmark Summary:");
            Console.WriteLine($"  Successful candidates: {report.Summary.SuccessfulCandidates}/{report.Summary.CandidateCount}");
            Console.WriteLine($"  Successful queries: {report.Summary.OverallSuccessCount}/{report.Summary.OverallQueryCount}");
            Console.WriteLine($"  Successful query rate: {report.Summary.OverallSuccessPercent}%");
            Console.WriteLine($"  Runtime capability hints: {ResolverReportTextFormatter.DescribeBenchmarkRuntimeCapabilities(report.Summary)}");
            for (int i = 0; i < report.Results.Length; i++) {
                Console.WriteLine(ResolverReportTextFormatter.BuildBenchmarkRankedLine(report.Results[i], i + 1, FormatDuration));
            }
            Console.WriteLine($"  Best endpoint: {ResolverReportTextFormatter.DescribeBenchmarkBest(report.Results, report.Summary, FormatDuration)}");
            WriteHumanLine($"  Policy result: {(evaluation.PolicyPassed ? "pass" : $"fail ({evaluation.PolicyReason})")}");

            int exitCode = GetBenchmarkExitCode(evaluation, options);
            if (options.BenchmarkSummaryLine) {
                Console.WriteLine(ResolverReportTextFormatter.BuildBenchmarkSummaryLine(report.Summary, exitCode));
            }

            if (!string.IsNullOrWhiteSpace(options.BenchmarkSavePath)) {
                SaveResolverScoreSnapshot(options.BenchmarkSavePath!, report.Snapshot);
            }

            return exitCode;
        }

        private static ResolverBenchmarkPolicy CreateBenchmarkPolicy(CliOptions options) {
            return new ResolverBenchmarkPolicy {
                MinSuccessPercent = options.BenchmarkMinSuccessPercent,
                MinSuccessfulCandidates = options.BenchmarkMinSuccessfulCandidates
            };
        }

        private static int GetBenchmarkExitCode(ResolverBenchmarkEvaluation evaluation, CliOptions options) {
            if (evaluation.SuccessfulCandidates == 0) {
                return 1;
            }

            if (options.BenchmarkMinSuccessfulCandidates.HasValue && evaluation.SuccessfulCandidates < options.BenchmarkMinSuccessfulCandidates.Value) {
                return 3;
            }

            if (options.BenchmarkMinSuccessPercent.HasValue && evaluation.OverallSuccessPercent < options.BenchmarkMinSuccessPercent.Value) {
                return 3;
            }

            return evaluation.PolicyPassed ? 0 : 1;
        }

        private static async Task<int> RunProbeAsync(CliOptions options, CancellationToken cancellationToken) {
            string domain = options.Domain!;
            ResolverExecutionTargetSource targetSource = CreateProbeTargetSource(options);
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, cancellationToken).ConfigureAwait(false);
            if (options.HasCustomEndpointInputs && targets.Length == 0) {
                Console.Error.WriteLine("No custom resolver endpoints were supplied.");
                return 1;
            }

            return await RunProbeWithTargetsAsync(
                options,
                targets,
                domain,
                GetProbeEndpointProfile(options),
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> RunProbeWithTargetsAsync(
            CliOptions options,
            ResolverExecutionTarget[] targets,
            string domain,
            string endpointProfile,
            CancellationToken cancellationToken) {
            if (options.OutputFormat == QueryOutputFormat.Json) {
                ResolverProbeReport jsonReport = await ExecuteProbeReportAsync(targets, domain, options, cancellationToken).ConfigureAwait(false);
                if (options.ProbeSavePath != null) {
                    SaveResolverScoreSnapshot(options.ProbeSavePath, jsonReport.Snapshot);
                }
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(jsonReport));
                return GetProbeExitCode(jsonReport.Evaluation, options);
            }
            Console.WriteLine("Probe:");
            WriteHumanLine($"  Domain: {domain}");
            WriteHumanLine($"  Endpoint profile: {endpointProfile}");
            Console.WriteLine($"  Candidates: {targets.Length}");
            Console.WriteLine($"  Detail mode: {(options.ProbeSummaryOnly ? "summary-only" : "full")}");

            ResolverProbeReport report = await ExecuteProbeReportAsync(targets, domain, options, cancellationToken).ConfigureAwait(false);
            if (!options.ProbeSummaryOnly) {
                foreach (ResolverProbeReportResult result in report.Results) {
                    WriteProbeResult(result);
                }
            }

            return WriteProbeSummary(report, options);
        }

        private static ResolverQueryRunOptions CreateQueryRunOptions(CliOptions options) {
            ResolverExecutionClientOptions clientOptions = CreateExecutionClientOptions(options, useBenchmarkTimeout: options.Benchmark);
            return new ResolverQueryRunOptions {
                TimeoutMs = options.Benchmark ? options.BenchmarkTimeoutMs : Configuration.DefaultTimeout,
                ConnectionMode = options.BenchmarkConnectionMode,
                RequestDnsSec = options.RequestDnsSec,
                ValidateDnsSec = options.ValidateDnsSec,
                MaxRetries = 1,
                RetryDelayMs = 0,
                PortOverride = clientOptions.PortOverride,
                ForceDohWirePost = clientOptions.ForceDohWirePost,
                BootstrapResolver = clientOptions.BootstrapResolver,
                DnsSecSignatureVerifier = clientOptions.DnsSecSignatureVerifier,
                RequestNsid = clientOptions.RequestNsid
            };
        }

        private static string[] GetEffectiveBenchmarkDomains(CliOptions options) {
            if (options.BenchmarkDomains.Count > 0) {
                return options.BenchmarkDomains.ToArray();
            }

            return string.IsNullOrWhiteSpace(options.Domain)
                ? Array.Empty<string>()
                : new[] { options.Domain! };
        }

        private static Task<ResolverBenchmarkReport> ExecuteBenchmarkReportAsync(
            ResolverExecutionTarget[] targets,
            string[] domains,
            DnsRecordType[] recordTypes,
            CliOptions options,
            CancellationToken cancellationToken) {
            return ResolverBenchmarkWorkflow.RunAsync(
                targets,
                domains,
                recordTypes,
                options.BenchmarkAttempts,
                options.BenchmarkConcurrency,
                options.BenchmarkTimeoutMs,
                CreateQueryRunOptions(options),
                CreateBenchmarkPolicy(options),
                builtInOverride: CreateBuiltInOverride(),
                explicitOverride: CreateExplicitOverride(),
                cancellationToken: cancellationToken);
        }

        private static ResolverExecutionTargetSource CreateBenchmarkTargetSource(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return new ResolverExecutionTargetSource {
                    ResolverSelectionPath = options.ResolverUsePath
                };
            }

            if (options.HasCustomEndpointInputs) {
                return new ResolverExecutionTargetSource {
                    ResolverEndpoints = options.ProbeEndpoints.ToArray(),
                    ResolverEndpointFiles = options.ProbeEndpointFiles.ToArray(),
                    ResolverEndpointUrls = options.ProbeEndpointUrls.ToArray()
                };
            }

            return new ResolverExecutionTargetSource {
                BuiltInEndpoints = options.BenchmarkEndpoints.Distinct().ToArray()
            };
        }

        private static Task<ResolverProbeReport> ExecuteProbeReportAsync(
            ResolverExecutionTarget[] targets,
            string domain,
            CliOptions options,
            CancellationToken cancellationToken) {
            return ResolverProbeWorkflow.RunAsync(
                targets,
                domain,
                options.RecordType,
                Configuration.DefaultTimeout,
                CreateQueryRunOptions(options),
                CreateProbePolicy(options),
                builtInOverride: CreateBuiltInOverride(),
                explicitOverride: CreateExplicitOverride(),
                cancellationToken: cancellationToken);
        }

        private static ResolverExecutionTargetSource CreateProbeTargetSource(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return new ResolverExecutionTargetSource {
                    ResolverSelectionPath = options.ResolverUsePath
                };
            }

            if (options.HasCustomEndpointInputs) {
                return new ResolverExecutionTargetSource {
                    ResolverEndpoints = options.ProbeEndpoints.ToArray(),
                    ResolverEndpointFiles = options.ProbeEndpointFiles.ToArray(),
                    ResolverEndpointUrls = options.ProbeEndpointUrls.ToArray()
                };
            }

            return new ResolverExecutionTargetSource {
                ProbeProfile = options.Endpoint
            };
        }

        private static string? GetBenchmarkEndpointProfile(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return "selected";
            }

            return options.HasCustomEndpointInputs ? "custom" : null;
        }

        private static string GetProbeEndpointProfile(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return "selected";
            }

            return options.HasCustomEndpointInputs ? "custom" : options.Endpoint.ToString();
        }

        private static Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? CreateBuiltInOverride() {
            if (ProbeOverride == null) {
                return null;
            }

            return async (endpoint, name, recordType, cancellationToken) => CreateOverrideAttemptResult(
                endpoint.ToString(),
                await ProbeOverride(endpoint, name, cancellationToken).ConfigureAwait(false));
        }

        private static Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? CreateExplicitOverride() {
            if (ProbeEndpointOverride == null) {
                return null;
            }

            return async (endpoint, name, recordType, cancellationToken) => CreateOverrideAttemptResult(
                ResolverExecutionPlanBuilder.DescribeEndpoint(endpoint),
                await ProbeEndpointOverride(endpoint, name, cancellationToken).ConfigureAwait(false));
        }

        private static ResolverQueryAttemptResult CreateOverrideAttemptResult(
            string target,
            (DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat) result) {
            return new ResolverQueryAttemptResult {
                Target = target,
                RequestFormat = result.RequestFormat,
                Resolver = result.Resolver,
                Response = result.Response,
                Elapsed = result.Elapsed
            };
        }

        private static void WriteProbeResult(ResolverProbeReportResult result) {
            foreach (string line in ResolverReportTextFormatter.BuildProbeResultLines(result, FormatDuration)) {
                Console.WriteLine(line);
            }
        }

        private static int WriteProbeSummary(ResolverProbeReport report, CliOptions options) {
            ResolverProbeEvaluation evaluation = report.Evaluation;
            ResolverProbeReportSummary summary = report.Summary;
            int successCount = summary.SuccessfulCandidates;
            Console.WriteLine("Probe Summary:");
            Console.WriteLine($"  Successful probes: {successCount}/{summary.CandidateCount}");
            Console.WriteLine($"  Failed probes: {summary.FailedCandidates}");
            Console.WriteLine($"  Fastest success: {ResolverReportTextFormatter.DescribeProbeFastestSuccess(summary, FormatDuration)}");
            Console.WriteLine($"  Fastest consensus responder: {ResolverReportTextFormatter.DescribeProbeFastestConsensus(summary, FormatDuration)}");
            Console.WriteLine($"  Transport coverage: {ResolverReportTextFormatter.DescribeProbeTransportCoverage(summary)}");
            Console.WriteLine($"  Runtime capability hints: {ResolverReportTextFormatter.DescribeProbeRuntimeCapabilities(summary)}");
            Console.WriteLine($"  Answer consensus: {ResolverReportTextFormatter.DescribeProbeAnswerConsensus(summary)}");
            Console.WriteLine($"  Mismatched responders: {ResolverReportTextFormatter.DescribeProbeMismatchedTargets(summary)}");
            Console.WriteLine($"  Distinct answer sets: {summary.DistinctAnswerSets}");
            Console.WriteLine($"  Answer variants: {ResolverReportTextFormatter.DescribeProbeAnswerVariants(summary)}");
            Console.WriteLine($"  Recommended endpoint: {ResolverReportTextFormatter.DescribeProbeRecommended(summary, FormatDuration)}");
            WriteHumanLine($"  Policy result: {(evaluation.PolicyPassed ? "pass" : $"fail ({evaluation.PolicyReason})")}");

            int exitCode = GetProbeExitCode(evaluation, options);
            if (options.ProbeSummaryLine) {
                Console.WriteLine(ResolverReportTextFormatter.BuildProbeSummaryLine(summary, exitCode));
            }

            if (!string.IsNullOrWhiteSpace(options.ProbeSavePath)) {
                SaveResolverScoreSnapshot(options.ProbeSavePath!, report.Snapshot);
            }

            return exitCode;
        }

        private static ResolverProbePolicy CreateProbePolicy(CliOptions options) {
            return new ResolverProbePolicy {
                MinSuccessCount = options.ProbeMinSuccessCount,
                MinSuccessPercent = options.ProbeMinSuccessPercent,
                MinConsensusPercent = options.ProbeMinConsensusPercent,
                RequireConsensus = options.ProbeRequireConsensus
            };
        }

        private static int GetProbeExitCode(ResolverProbeEvaluation evaluation, CliOptions options) {
            if (evaluation.SuccessfulCandidates == 0) {
                return 1;
            }

            if (options.ProbeMinSuccessCount.HasValue && evaluation.SuccessfulCandidates < options.ProbeMinSuccessCount.Value) {
                return 3;
            }

            if (options.ProbeMinSuccessPercent.HasValue && evaluation.SuccessPercent < options.ProbeMinSuccessPercent.Value) {
                return 3;
            }

            if (options.ProbeRequireConsensus && evaluation.DistinctAnswerSets > 1) {
                return 2;
            }

            if (options.ProbeMinConsensusPercent.HasValue && evaluation.ConsensusPercent < options.ProbeMinConsensusPercent.Value) {
                return 2;
            }

            return evaluation.PolicyPassed ? 0 : 1;
        }

    }
}
