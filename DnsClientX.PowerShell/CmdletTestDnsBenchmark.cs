using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// Per-candidate DNS benchmark output for PowerShell consumers.
    /// </summary>
    public sealed class DnsBenchmarkResult {
        /// <summary>
        /// Candidate label, such as a provider name or explicit transport endpoint.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Resolver address observed on the fastest successful attempt for this candidate.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Actual transport observed on the fastest successful attempt for this candidate.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// Domain names included in the benchmark matrix.
        /// </summary>
        public string[] Domains { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Record types included in the benchmark matrix.
        /// </summary>
        public DnsRecordType[] RecordTypes { get; set; } = Array.Empty<DnsRecordType>();

        /// <summary>
        /// Number of attempts executed for each domain/type combination.
        /// </summary>
        public int AttemptsPerCombination { get; set; }

        /// <summary>
        /// Maximum concurrent in-flight benchmark queries used for the run.
        /// </summary>
        public int MaxConcurrency { get; set; }

        /// <summary>
        /// Per-query timeout in milliseconds used for the run.
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Total query attempts for this candidate.
        /// </summary>
        public int TotalQueries { get; set; }

        /// <summary>
        /// Successful query count for this candidate.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Failed query count for this candidate.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Successful queries as a percentage of <see cref="TotalQueries"/>.
        /// </summary>
        public int SuccessPercent { get; set; }

        /// <summary>
        /// Average successful round-trip time in milliseconds.
        /// </summary>
        public double AverageMs { get; set; }

        /// <summary>
        /// Minimum successful round-trip time in milliseconds.
        /// </summary>
        public double MinMs { get; set; }

        /// <summary>
        /// Maximum successful round-trip time in milliseconds.
        /// </summary>
        public double MaxMs { get; set; }

        /// <summary>
        /// Number of distinct answer sets observed among successful attempts.
        /// </summary>
        public int DistinctAnswerSets { get; set; }

        /// <summary>
        /// Ranking position across all candidates in this benchmark run.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Indicates whether this candidate was the best successful result in the run.
        /// </summary>
        public bool IsBest { get; set; }

        /// <summary>
        /// Indicates whether this candidate is recommended under the current benchmark policy.
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Indicates whether the overall benchmark policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Explains why the overall benchmark policy failed, or <c>none</c> when it passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";

        /// <summary>
        /// Minimum overall success percentage required by policy, if any.
        /// </summary>
        public int? RequiredMinSuccessPercent { get; set; }

        /// <summary>
        /// Minimum successful candidate count required by policy, if any.
        /// </summary>
        public int? RequiredMinSuccessfulCandidates { get; set; }

        /// <summary>
        /// Total candidate count in the run.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Number of candidates with at least one successful query.
        /// </summary>
        public int SuccessfulCandidates { get; set; }

        /// <summary>
        /// Overall successful query percentage across all candidates.
        /// </summary>
        public int OverallSuccessPercent { get; set; }

        /// <summary>
        /// Total successful queries across all candidates.
        /// </summary>
        public int OverallSuccessCount { get; set; }

        /// <summary>
        /// Total queries across all candidates.
        /// </summary>
        public int OverallQueryCount { get; set; }
    }

    /// <summary>
    /// Run-level DNS benchmark summary for PowerShell consumers.
    /// </summary>
    public sealed class DnsBenchmarkSummary {
        /// <summary>
        /// Domain names included in the benchmark matrix.
        /// </summary>
        public string[] Domains { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Record types included in the benchmark matrix.
        /// </summary>
        public DnsRecordType[] RecordTypes { get; set; } = Array.Empty<DnsRecordType>();

        /// <summary>
        /// Number of attempts executed for each domain/type combination.
        /// </summary>
        public int AttemptsPerCombination { get; set; }

        /// <summary>
        /// Maximum concurrent in-flight benchmark queries used for the run.
        /// </summary>
        public int MaxConcurrency { get; set; }

        /// <summary>
        /// Per-query timeout in milliseconds used for the run.
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Total candidate count in the run.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Number of candidates with at least one successful query.
        /// </summary>
        public int SuccessfulCandidates { get; set; }

        /// <summary>
        /// Total successful queries across all candidates.
        /// </summary>
        public int OverallSuccessCount { get; set; }

        /// <summary>
        /// Total queries across all candidates.
        /// </summary>
        public int OverallQueryCount { get; set; }

        /// <summary>
        /// Overall successful query percentage across all candidates.
        /// </summary>
        public int OverallSuccessPercent { get; set; }

        /// <summary>
        /// Indicates whether the overall benchmark policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Explains why the overall benchmark policy failed, or <c>none</c> when it passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";

        /// <summary>
        /// Minimum overall success percentage required by policy, if any.
        /// </summary>
        public int? RequiredMinSuccessPercent { get; set; }

        /// <summary>
        /// Minimum successful candidate count required by policy, if any.
        /// </summary>
        public int? RequiredMinSuccessfulCandidates { get; set; }

        /// <summary>
        /// Best successful benchmark target under the current run.
        /// </summary>
        public string RecommendedTarget { get; set; } = "none";

        /// <summary>
        /// Resolver observed on the recommended target's fastest successful attempt.
        /// </summary>
        public string RecommendedResolver { get; set; } = "none";

        /// <summary>
        /// Transport observed on the recommended target's fastest successful attempt.
        /// </summary>
        public string RecommendedTransport { get; set; } = "none";

        /// <summary>
        /// Average successful latency for the recommended target in milliseconds.
        /// </summary>
        public double RecommendedAverageMs { get; set; }

        /// <summary>
        /// Indicates whether a recommendation is available under the current policy.
        /// </summary>
        public bool RecommendationAvailable { get; set; }

        /// <summary>
        /// Number of unique candidates blocked by runtime transport support.
        /// </summary>
        public int RuntimeUnsupportedCandidateCount { get; set; }

        /// <summary>
        /// Runtime capability warnings captured during the benchmark run.
        /// </summary>
        public string[] RuntimeCapabilityWarnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// <para type="synopsis">Benchmarks one or more DNS providers or explicit resolver endpoints across repeated queries.</para>
    /// <para type="description">Returns one object per candidate with latency, success rate, answer consistency, rank, and recommendation metadata. PowerShell consumers can sort, filter, format, or export the results themselves.</para>
    /// <example>
    ///   <para>Benchmark three built-in providers with repeated A lookups</para>
    ///   <code>Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Quad9,Google -Attempts 5</code>
    /// </example>
    /// <example>
    ///   <para>Benchmark a custom resolver matrix across domains and record types</para>
    ///   <code>Test-DnsBenchmark -Name example.com,microsoft.com -Type A,AAAA -ResolverEndpoint 'udp@1.1.1.1:53','tcp@9.9.9.9:53' -Attempts 3 -MaxConcurrency 8</code>
    /// </example>
    /// <example>
    ///   <para>Benchmark modern transports without changing the core package graph</para>
    ///   <code>Test-DnsBenchmark -Name example.com -ResolverEndpoint 'doq@dns.quad9.net:853','doh3@https://dns.quad9.net/dns-query' -Attempts 2 -SummaryOnly</code>
    /// </example>
    /// <example>
    ///   <para>Require strong benchmark health before recommending a winner</para>
    ///   <code>Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Quad9 -MinSuccessPercent 90 -MinSuccessfulCandidates 2</code>
    /// </example>
    /// <example>
    ///   <para>Include per-candidate rows plus one run-level summary object</para>
    ///   <code>Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Google -Attempts 3 -IncludeSummary</code>
    /// </example>
    /// <example>
    ///   <para>Return only the run-level summary object for automation</para>
    ///   <code>Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Google -Attempts 3 -SummaryOnly</code>
    /// </example>
    /// <example>
    ///   <para>Benchmark explicit endpoints and keep only the recommended summary for automation</para>
    ///   <code>Test-DnsBenchmark -Name example.com,microsoft.com -Type A,AAAA -ResolverEndpoint 'udp@1.1.1.1:53','tcp@9.9.9.9:53' -Attempts 2 -SummaryOnly</code>
    /// </example>
    /// <example>
    ///   <para>Reuse the recommended resolver from a saved score snapshot as the single benchmark candidate</para>
    ///   <code>Test-DnsBenchmark -Name example.com -ResolverSelectionPath '.\resolver-score.json' -Attempts 3 -SummaryOnly</code>
    /// </example>
    /// <example>
    ///   <para>Benchmark resolvers and persist the scored recommendation snapshot for later reuse</para>
    ///   <code>Test-DnsBenchmark -Name example.com -DnsProvider Cloudflare,Google -Attempts 3 -SavePath '.\resolver-score.json' -IncludeSummary</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "DnsBenchmark", DefaultParameterSetName = "DnsProvider")]
    [OutputType(typeof(DnsBenchmarkResult), typeof(DnsBenchmarkSummary))]
    public sealed class CmdletTestDnsBenchmark : AsyncPSCmdlet {
        /// <summary>
        /// Domain names to benchmark.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverSelection")]
        public string[] Name { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Record types to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverSelection")]
        public DnsRecordType[] Type { get; set; } = [DnsRecordType.A];

        /// <summary>
        /// Built-in provider candidates to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        public DnsEndpoint[] DnsProvider { get; set; } = [DnsEndpoint.System];

        /// <summary>
        /// Explicit resolver endpoint candidates to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Files containing resolver endpoint candidates to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpointFile { get; set; } = Array.Empty<string>();

        /// <summary>
        /// HTTP or HTTPS URLs exposing resolver endpoint candidates to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpointUrl { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Path to a saved resolver score snapshot whose recommended resolver should be reused as the single benchmark candidate.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverSelection")]
        public string ResolverSelectionPath { get; set; } = string.Empty;

        /// <summary>
        /// Number of attempts per domain/type combination.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int Attempts { get; set; } = 3;

        /// <summary>
        /// Maximum concurrent in-flight benchmark queries across the whole run.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int MaxConcurrency { get; set; } = 4;

        /// <summary>
        /// Per-query timeout in milliseconds.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int TimeOut { get; set; } = Configuration.DefaultTimeout;

        /// <summary>
        /// Request DNSSEC records by setting the DO bit.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public SwitchParameter RequestDnsSec { get; set; }

        /// <summary>
        /// Validate DNSSEC signatures. Implies <see cref="RequestDnsSec"/>.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public SwitchParameter ValidateDnsSec { get; set; }

        /// <summary>
        /// Require a minimum overall successful query percentage for the run to pass policy.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int? MinSuccessPercent { get; set; }

        /// <summary>
        /// Require a minimum number of healthy candidates with at least one successful query.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int? MinSuccessfulCandidates { get; set; }

        /// <summary>
        /// Emits a run-level summary object after the per-candidate benchmark results.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public SwitchParameter IncludeSummary { get; set; }

        /// <summary>
        /// Emits only the run-level summary object and suppresses per-candidate rows.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public SwitchParameter SummaryOnly { get; set; }

        /// <summary>
        /// Optional path where the benchmark score snapshot should be saved for later selection and reuse.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public string SavePath { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            ValidateParameters();

            string[] names = Name
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length == 0) {
                throw new PSArgumentException("At least one non-empty domain name is required.", nameof(Name));
            }

            DnsRecordType[] recordTypes = Type.Length == 0
                ? [DnsRecordType.A]
                : Type.Distinct().ToArray();

            ResolverExecutionTargetSource targetSource = CreateTargetSource();
            ResolverExecutionTarget[] candidates = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, CancelToken).ConfigureAwait(false);
            if (candidates.Length == 0) {
                WriteWarning("No benchmark candidates were produced from the supplied input.");
                return;
            }

            WriteVerbose($"Benchmarking {candidates.Length} candidate(s) across {names.Length} domain(s), {recordTypes.Length} record type(s), and {Attempts} attempt(s) per combination.");

            ResolverBenchmarkReport report = await RunBenchmarkAsync(candidates, names, recordTypes, CancelToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(SavePath)) {
                ResolverScoreStore.Save(SavePath, report.Snapshot);
                WriteVerbose($"Saved resolver score snapshot to {SavePath}.");
            }
            if (!SummaryOnly.IsPresent) {
                WriteObject(report.Results.Select(ToDnsBenchmarkResult).ToArray(), true);
            }
            if (IncludeSummary.IsPresent) {
                WriteObject(ToDnsBenchmarkSummary(report.Summary));
            }
        }

        private ResolverBenchmarkPolicy CreateBenchmarkPolicy() {
            return new ResolverBenchmarkPolicy {
                MinSuccessPercent = MinSuccessPercent,
                MinSuccessfulCandidates = MinSuccessfulCandidates
            };
        }

        private void ValidateParameters() {
            if (Attempts < 1) {
                throw new PSArgumentOutOfRangeException(nameof(Attempts), Attempts, "Attempts must be at least 1.");
            }

            if (MaxConcurrency < 1) {
                throw new PSArgumentOutOfRangeException(nameof(MaxConcurrency), MaxConcurrency, "MaxConcurrency must be at least 1.");
            }

            if (TimeOut < 1) {
                throw new PSArgumentOutOfRangeException(nameof(TimeOut), TimeOut, "TimeOut must be at least 1.");
            }

            if (MinSuccessPercent.HasValue && (MinSuccessPercent.Value < 1 || MinSuccessPercent.Value > 100)) {
                throw new PSArgumentOutOfRangeException(nameof(MinSuccessPercent), MinSuccessPercent.Value, "MinSuccessPercent must be between 1 and 100.");
            }

            if (MinSuccessfulCandidates.HasValue && MinSuccessfulCandidates.Value < 1) {
                throw new PSArgumentOutOfRangeException(nameof(MinSuccessfulCandidates), MinSuccessfulCandidates.Value, "MinSuccessfulCandidates must be at least 1.");
            }

            if (SummaryOnly.IsPresent) {
                IncludeSummary = true;
            }

            if (ParameterSetName == "ResolverEndpoint" &&
                (ResolverEndpoint?.Length ?? 0) == 0 &&
                (ResolverEndpointFile?.Length ?? 0) == 0 &&
                (ResolverEndpointUrl?.Length ?? 0) == 0) {
                throw new PSArgumentException(
                    "At least one resolver endpoint, resolver endpoint file, or resolver endpoint URL must be specified.",
                    nameof(ResolverEndpoint));
            }
        }

        private ResolverExecutionTargetSource CreateTargetSource() {
            if (ParameterSetName == "ResolverSelection") {
                return new ResolverExecutionTargetSource {
                    ResolverSelectionPath = ResolverSelectionPath
                };
            }

            if (ParameterSetName == "ResolverEndpoint") {
                return new ResolverExecutionTargetSource {
                    ResolverEndpoints = ResolverEndpoint.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray(),
                    ResolverEndpointFiles = ResolverEndpointFile,
                    ResolverEndpointUrls = ResolverEndpointUrl
                };
            }

            return new ResolverExecutionTargetSource {
                BuiltInEndpoints = DnsProvider.Distinct().ToArray()
            };
        }

        private async Task<ResolverBenchmarkReport> RunBenchmarkAsync(IReadOnlyList<ResolverExecutionTarget> candidates, string[] names, DnsRecordType[] recordTypes, CancellationToken cancellationToken) {
            ResolverBenchmarkReport report = await ResolverBenchmarkWorkflow.RunAsync(
                candidates,
                names,
                recordTypes,
                Attempts,
                MaxConcurrency,
                TimeOut,
                CreateQueryRunOptions(),
                CreateBenchmarkPolicy(),
                progress: (completed, total) => {
                    int percent = total == 0
                        ? 100
                        : (int)Math.Round((double)completed * 100 / total, MidpointRounding.AwayFromZero);
                    WriteProgress(new ProgressRecord(1, "Testing DNS benchmark candidates", $"{completed}/{total} queries completed") {
                        PercentComplete = percent
                    });
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            WriteProgress(new ProgressRecord(1, "Testing DNS benchmark candidates", "Completed") {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            });

            if (!report.Evaluation.PolicyPassed) {
                WriteWarning($"Benchmark policy failed: {report.Evaluation.PolicyReason}");
            } else {
                WriteVerbose("Benchmark policy passed.");
            }

            return report;
        }

        private ResolverQueryRunOptions CreateQueryRunOptions() {
            return new ResolverQueryRunOptions {
                TimeoutMs = TimeOut,
                RequestDnsSec = RequestDnsSec.IsPresent || ValidateDnsSec.IsPresent,
                ValidateDnsSec = ValidateDnsSec.IsPresent,
                MaxRetries = 1,
                RetryDelayMs = 0
            };
        }

        private static DnsBenchmarkResult ToDnsBenchmarkResult(ResolverBenchmarkReportResult result) {
            return new DnsBenchmarkResult {
                Target = result.Target,
                Resolver = result.Resolver,
                Transport = result.Transport,
                Domains = result.Domains,
                RecordTypes = result.RecordTypes,
                AttemptsPerCombination = result.AttemptsPerCombination,
                MaxConcurrency = result.MaxConcurrency,
                TimeoutMs = result.TimeoutMs,
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
                IsRecommended = result.IsRecommended,
                PolicyPassed = result.PolicyPassed,
                PolicyReason = result.PolicyReason,
                RequiredMinSuccessPercent = result.RequiredMinSuccessPercent,
                RequiredMinSuccessfulCandidates = result.RequiredMinSuccessfulCandidates,
                CandidateCount = result.CandidateCount,
                SuccessfulCandidates = result.SuccessfulCandidates,
                OverallSuccessPercent = result.OverallSuccessPercent,
                OverallSuccessCount = result.OverallSuccessCount,
                OverallQueryCount = result.OverallQueryCount
            };
        }

        private static DnsBenchmarkSummary ToDnsBenchmarkSummary(ResolverBenchmarkReportSummary summary) {
            return new DnsBenchmarkSummary {
                Domains = summary.Domains,
                RecordTypes = summary.RecordTypes,
                AttemptsPerCombination = summary.AttemptsPerCombination,
                MaxConcurrency = summary.MaxConcurrency,
                TimeoutMs = summary.TimeoutMs,
                CandidateCount = summary.CandidateCount,
                SuccessfulCandidates = summary.SuccessfulCandidates,
                OverallSuccessCount = summary.OverallSuccessCount,
                OverallQueryCount = summary.OverallQueryCount,
                OverallSuccessPercent = summary.OverallSuccessPercent,
                PolicyPassed = summary.PolicyPassed,
                PolicyReason = summary.PolicyReason,
                RequiredMinSuccessPercent = summary.RequiredMinSuccessPercent,
                RequiredMinSuccessfulCandidates = summary.RequiredMinSuccessfulCandidates,
                RecommendedTarget = summary.RecommendedTarget,
                RecommendedResolver = summary.RecommendedResolver,
                RecommendedTransport = summary.RecommendedTransport,
                RecommendedAverageMs = summary.RecommendedAverageMs,
                RecommendationAvailable = summary.RecommendationAvailable,
                RuntimeUnsupportedCandidateCount = summary.RuntimeUnsupportedCandidateCount,
                RuntimeCapabilityWarnings = summary.RuntimeCapabilityWarnings
            };
        }

    }
}
