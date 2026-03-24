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
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "DnsBenchmark", DefaultParameterSetName = "DnsProvider")]
    [OutputType(typeof(DnsBenchmarkResult), typeof(DnsBenchmarkSummary))]
    public sealed class CmdletTestDnsBenchmark : AsyncPSCmdlet {
        private sealed class BenchmarkCandidate {
            public string DisplayName { get; set; } = string.Empty;
            public Func<string, DnsRecordType, CancellationToken, Task<BenchmarkAttemptResult>> Runner { get; set; } = null!;
        }

        private sealed class BenchmarkAttemptEnvelope {
            public int CandidateIndex { get; set; }
            public BenchmarkAttemptResult Attempt { get; set; } = null!;
        }

        private sealed class BenchmarkAttemptResult {
            public bool Succeeded { get; set; }
            public TimeSpan Elapsed { get; set; }
            public string Resolver { get; set; } = "none";
            public string Transport { get; set; } = "none";
            public string AnswerSignature { get; set; } = "(no answers)";
        }

        private sealed class BenchmarkPolicyOutcome {
            public bool Passed { get; set; }
            public string Reason { get; set; } = "none";
        }

        private sealed class BenchmarkRunResult {
            public DnsBenchmarkResult[] RankedResults { get; set; } = Array.Empty<DnsBenchmarkResult>();
            public DnsBenchmarkSummary Summary { get; set; } = new DnsBenchmarkSummary();
        }

        /// <summary>
        /// Domain names to benchmark.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverEndpoint")]
        public string[] Name { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Record types to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverEndpoint")]
        public DnsRecordType[] Type { get; set; } = [DnsRecordType.A];

        /// <summary>
        /// Built-in provider candidates to benchmark.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        public DnsEndpoint[] DnsProvider { get; set; } = [DnsEndpoint.System];

        /// <summary>
        /// Explicit resolver endpoint candidates to benchmark.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Number of attempts per domain/type combination.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public int Attempts { get; set; } = 3;

        /// <summary>
        /// Maximum concurrent in-flight benchmark queries across the whole run.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public int MaxConcurrency { get; set; } = 4;

        /// <summary>
        /// Per-query timeout in milliseconds.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public int TimeOut { get; set; } = Configuration.DefaultTimeout;

        /// <summary>
        /// Request DNSSEC records by setting the DO bit.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public SwitchParameter RequestDnsSec { get; set; }

        /// <summary>
        /// Validate DNSSEC signatures. Implies <see cref="RequestDnsSec"/>.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public SwitchParameter ValidateDnsSec { get; set; }

        /// <summary>
        /// Require a minimum overall successful query percentage for the run to pass policy.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public int? MinSuccessPercent { get; set; }

        /// <summary>
        /// Require a minimum number of healthy candidates with at least one successful query.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public int? MinSuccessfulCandidates { get; set; }

        /// <summary>
        /// Emits a run-level summary object after the per-candidate benchmark results.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public SwitchParameter IncludeSummary { get; set; }

        /// <summary>
        /// Emits only the run-level summary object and suppresses per-candidate rows.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public SwitchParameter SummaryOnly { get; set; }

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

            BenchmarkCandidate[] candidates = CreateCandidates();
            if (candidates.Length == 0) {
                WriteWarning("No benchmark candidates were produced from the supplied input.");
                return;
            }

            WriteVerbose($"Benchmarking {candidates.Length} candidate(s) across {names.Length} domain(s), {recordTypes.Length} record type(s), and {Attempts} attempt(s) per combination.");

            BenchmarkRunResult report = await RunBenchmarkAsync(candidates, names, recordTypes, CancelToken).ConfigureAwait(false);
            if (!SummaryOnly.IsPresent) {
                WriteObject(report.RankedResults, true);
            }
            if (IncludeSummary.IsPresent) {
                WriteObject(report.Summary);
            }
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
        }

        private BenchmarkCandidate[] CreateCandidates() {
            if (ParameterSetName == "ResolverEndpoint") {
                DnsResolverEndpoint[] endpoints = EndpointParser.TryParseMany(
                    ResolverEndpoint.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()),
                    out IReadOnlyList<string> errors);

                if (errors.Count > 0) {
                    throw new PSArgumentException(string.Join("; ", errors), nameof(ResolverEndpoint));
                }

                return endpoints
                    .GroupBy(DescribeEndpoint, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Select(endpoint => new BenchmarkCandidate {
                        DisplayName = DescribeEndpoint(endpoint),
                        Runner = (name, type, token) => BenchmarkEndpointAsync(endpoint, name, type, token)
                    })
                    .ToArray();
            }

            return DnsProvider
                .Distinct()
                .Select(endpoint => new BenchmarkCandidate {
                    DisplayName = endpoint.ToString(),
                    Runner = (name, type, token) => BenchmarkEndpointAsync(endpoint, name, type, token)
                })
                .ToArray();
        }

        private async Task<BenchmarkRunResult> RunBenchmarkAsync(IReadOnlyList<BenchmarkCandidate> candidates, string[] names, DnsRecordType[] recordTypes, CancellationToken cancellationToken) {
            int totalQueries = candidates.Count * names.Length * recordTypes.Length * Attempts;
            int completed = 0;
            using var semaphore = new SemaphoreSlim(MaxConcurrency);
            var tasks = new List<Task<BenchmarkAttemptEnvelope>>(totalQueries);

            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++) {
                BenchmarkCandidate candidate = candidates[candidateIndex];
                foreach (string name in names) {
                    foreach (DnsRecordType recordType in recordTypes) {
                        for (int attempt = 0; attempt < Attempts; attempt++) {
                            tasks.Add(RunBenchmarkAttemptAsync(candidateIndex, candidate, name, recordType, semaphore, totalQueries, () => Interlocked.Increment(ref completed), cancellationToken));
                        }
                    }
                }
            }

            BenchmarkAttemptEnvelope[] attempts = await Task.WhenAll(tasks).ConfigureAwait(false);
            WriteProgress(new ProgressRecord(1, "Testing DNS benchmark candidates", "Completed") {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            });

            var results = new List<DnsBenchmarkResult>(candidates.Count);
            foreach (var group in attempts.GroupBy(item => item.CandidateIndex).OrderBy(group => group.Key)) {
                BenchmarkCandidate candidate = candidates[group.Key];
                results.Add(BuildResult(candidate.DisplayName, group.Select(item => item.Attempt).ToArray(), names, recordTypes));
            }

            DnsBenchmarkResult[] ranked = results
                .OrderByDescending(result => result.SuccessPercent)
                .ThenBy(result => result.SuccessCount == 0 ? double.MaxValue : result.AverageMs)
                .ThenBy(result => result.Target, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            DnsBenchmarkResult? best = ranked.FirstOrDefault(result => result.SuccessCount > 0);
            BenchmarkPolicyOutcome policy = EvaluatePolicy(ranked);
            int successfulCandidates = ranked.Count(result => result.SuccessCount > 0);
            int successfulQueries = ranked.Sum(result => result.SuccessCount);
            int overallQueryCount = ranked.Sum(result => result.TotalQueries);
            int overallSuccessPercent = overallQueryCount == 0
                ? 0
                : (int)Math.Round((double)successfulQueries * 100 / overallQueryCount, MidpointRounding.AwayFromZero);

            if (!policy.Passed) {
                WriteWarning($"Benchmark policy failed: {policy.Reason}");
            } else {
                WriteVerbose("Benchmark policy passed.");
            }

            for (int i = 0; i < ranked.Length; i++) {
                ranked[i].Rank = i + 1;
                ranked[i].IsBest = best != null && string.Equals(ranked[i].Target, best.Target, StringComparison.OrdinalIgnoreCase);
                ranked[i].IsRecommended = policy.Passed && ranked[i].IsBest;
                ranked[i].PolicyPassed = policy.Passed;
                ranked[i].PolicyReason = policy.Passed ? "none" : policy.Reason;
                ranked[i].CandidateCount = ranked.Length;
                ranked[i].SuccessfulCandidates = successfulCandidates;
                ranked[i].OverallSuccessPercent = overallSuccessPercent;
                ranked[i].OverallSuccessCount = successfulQueries;
                ranked[i].OverallQueryCount = overallQueryCount;
            }

            return new BenchmarkRunResult {
                RankedResults = ranked,
                Summary = new DnsBenchmarkSummary {
                    Domains = names,
                    RecordTypes = recordTypes,
                    AttemptsPerCombination = Attempts,
                    MaxConcurrency = MaxConcurrency,
                    TimeoutMs = TimeOut,
                    CandidateCount = ranked.Length,
                    SuccessfulCandidates = successfulCandidates,
                    OverallSuccessCount = successfulQueries,
                    OverallQueryCount = overallQueryCount,
                    OverallSuccessPercent = overallSuccessPercent,
                    PolicyPassed = policy.Passed,
                    PolicyReason = policy.Passed ? "none" : policy.Reason,
                    RequiredMinSuccessPercent = MinSuccessPercent,
                    RequiredMinSuccessfulCandidates = MinSuccessfulCandidates,
                    RecommendedTarget = best?.Target ?? "none",
                    RecommendedResolver = best?.Resolver ?? "none",
                    RecommendedTransport = best?.Transport ?? "none",
                    RecommendedAverageMs = best?.AverageMs ?? 0,
                    RecommendationAvailable = policy.Passed && best != null
                }
            };
        }

        private async Task<BenchmarkAttemptEnvelope> RunBenchmarkAttemptAsync(int candidateIndex, BenchmarkCandidate candidate, string name, DnsRecordType recordType, SemaphoreSlim semaphore, int totalQueries, Func<int> markCompleted, CancellationToken cancellationToken) {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                BenchmarkAttemptResult result = await candidate.Runner(name, recordType, cancellationToken).ConfigureAwait(false);
                return new BenchmarkAttemptEnvelope {
                    CandidateIndex = candidateIndex,
                    Attempt = result
                };
            } finally {
                int finished = markCompleted();
                int percent = totalQueries == 0
                    ? 100
                    : (int)Math.Round((double)finished * 100 / totalQueries, MidpointRounding.AwayFromZero);
                WriteProgress(new ProgressRecord(1, "Testing DNS benchmark candidates", $"{finished}/{totalQueries} queries completed") {
                    PercentComplete = percent
                });
                semaphore.Release();
            }
        }

        private DnsBenchmarkResult BuildResult(string displayName, IReadOnlyList<BenchmarkAttemptResult> attempts, string[] names, DnsRecordType[] recordTypes) {
            BenchmarkAttemptResult[] successful = attempts.Where(result => result.Succeeded).ToArray();
            BenchmarkAttemptResult? fastest = successful
                .OrderBy(result => result.Elapsed)
                .FirstOrDefault();
            int totalQueries = attempts.Count;
            int successCount = successful.Length;
            int failureCount = totalQueries - successCount;
            int successPercent = totalQueries == 0
                ? 0
                : (int)Math.Round((double)successCount * 100 / totalQueries, MidpointRounding.AwayFromZero);

            return new DnsBenchmarkResult {
                Target = displayName,
                Resolver = fastest?.Resolver ?? "none",
                Transport = fastest?.Transport ?? "none",
                Domains = names,
                RecordTypes = recordTypes,
                AttemptsPerCombination = Attempts,
                TotalQueries = totalQueries,
                SuccessCount = successCount,
                FailureCount = failureCount,
                SuccessPercent = successPercent,
                AverageMs = successCount == 0 ? 0 : Math.Round(successful.Average(result => result.Elapsed.TotalMilliseconds), 2, MidpointRounding.AwayFromZero),
                MinMs = successCount == 0 ? 0 : Math.Round(successful.Min(result => result.Elapsed.TotalMilliseconds), 2, MidpointRounding.AwayFromZero),
                MaxMs = successCount == 0 ? 0 : Math.Round(successful.Max(result => result.Elapsed.TotalMilliseconds), 2, MidpointRounding.AwayFromZero),
                DistinctAnswerSets = successCount == 0 ? 0 : successful.GroupBy(result => result.AnswerSignature, StringComparer.Ordinal).Count()
            };
        }

        private BenchmarkPolicyOutcome EvaluatePolicy(IReadOnlyList<DnsBenchmarkResult> results) {
            int successfulCandidates = results.Count(result => result.SuccessCount > 0);
            int totalQueries = results.Sum(result => result.TotalQueries);
            int successfulQueries = results.Sum(result => result.SuccessCount);
            int successPercent = totalQueries == 0
                ? 0
                : (int)Math.Round((double)successfulQueries * 100 / totalQueries, MidpointRounding.AwayFromZero);

            if (successfulCandidates == 0) {
                return new BenchmarkPolicyOutcome {
                    Passed = false,
                    Reason = "no successful candidates"
                };
            }

            if (MinSuccessfulCandidates.HasValue && successfulCandidates < MinSuccessfulCandidates.Value) {
                return new BenchmarkPolicyOutcome {
                    Passed = false,
                    Reason = $"successful candidates {successfulCandidates}/{results.Count} below required count {MinSuccessfulCandidates.Value}"
                };
            }

            if (MinSuccessPercent.HasValue && successPercent < MinSuccessPercent.Value) {
                return new BenchmarkPolicyOutcome {
                    Passed = false,
                    Reason = $"success rate {successPercent}% below required {MinSuccessPercent.Value}%"
                };
            }

            return new BenchmarkPolicyOutcome {
                Passed = true
            };
        }

        private async Task<BenchmarkAttemptResult> BenchmarkEndpointAsync(DnsEndpoint endpoint, string name, DnsRecordType recordType, CancellationToken cancellationToken) {
            using var client = new ClientX(endpoint);
            ConfigureClient(client);
            return await ExecuteAttemptAsync(client, name, recordType, cancellationToken).ConfigureAwait(false);
        }

        private async Task<BenchmarkAttemptResult> BenchmarkEndpointAsync(DnsResolverEndpoint endpoint, string name, DnsRecordType recordType, CancellationToken cancellationToken) {
            using var client = CreateClient(endpoint);
            ConfigureClient(client, endpoint);
            return await ExecuteAttemptAsync(client, name, recordType, cancellationToken).ConfigureAwait(false);
        }

        private void ConfigureClient(ClientX client, DnsResolverEndpoint? endpoint = null) {
            client.EndpointConfiguration.TimeOut = TimeOut;
            if (endpoint?.Port > 0 == true) {
                client.EndpointConfiguration.Port = endpoint.Port;
            }
        }

        private async Task<BenchmarkAttemptResult> ExecuteAttemptAsync(ClientX client, string name, DnsRecordType recordType, CancellationToken cancellationToken) {
            bool requestDnsSec = RequestDnsSec.IsPresent || ValidateDnsSec.IsPresent;
            bool validateDnsSec = ValidateDnsSec.IsPresent;
            var stopwatch = Stopwatch.StartNew();

            try {
                DnsResponse response = await client.Resolve(
                    name,
                    recordType,
                    requestDnsSec,
                    validateDnsSec,
                    retryOnTransient: false,
                    maxRetries: 1,
                    retryDelayMs: 0,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                bool succeeded = response.Status == DnsResponseCode.NoError && string.IsNullOrWhiteSpace(response.Error);
                TimeSpan elapsed = response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : stopwatch.Elapsed;
                return new BenchmarkAttemptResult {
                    Succeeded = succeeded,
                    Elapsed = elapsed,
                    Resolver = !string.IsNullOrWhiteSpace(response.ServerAddress) ? response.ServerAddress! : "none",
                    Transport = response.UsedTransport.ToString(),
                    AnswerSignature = BuildAnswerSignature(response)
                };
            } catch (Exception ex) {
                stopwatch.Stop();
                WriteVerbose($"Benchmark attempt failed for {name} {recordType}: {ex.Message}");
                return new BenchmarkAttemptResult {
                    Succeeded = false,
                    Elapsed = stopwatch.Elapsed,
                    Resolver = "none",
                    Transport = "none",
                    AnswerSignature = "(no answers)"
                };
            }
        }

        private static ClientX CreateClient(DnsResolverEndpoint endpoint) {
            if (endpoint.Transport == Transport.Doh) {
                Uri dohUri = EndpointParser.BuildDohUri(endpoint);
                return new ClientX(dohUri, MapTransport(endpoint.Transport));
            }

            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("Custom benchmark endpoint requires Host.", nameof(endpoint));
            }

            var client = new ClientX(endpoint.Host!, MapTransport(endpoint.Transport));
            client.EndpointConfiguration.Port = endpoint.Port;
            return client;
        }

        private static DnsRequestFormat MapTransport(Transport transport) {
            return transport switch {
                Transport.Udp => DnsRequestFormat.DnsOverUDP,
                Transport.Tcp => DnsRequestFormat.DnsOverTCP,
                Transport.Dot => DnsRequestFormat.DnsOverTLS,
                Transport.Doh => DnsRequestFormat.DnsOverHttps,
                Transport.Quic => DnsRequestFormat.DnsOverQuic,
                Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
                Transport.Multicast => DnsRequestFormat.Multicast,
                _ => DnsRequestFormat.DnsOverUDP
            };
        }

        private static string DescribeEndpoint(DnsResolverEndpoint endpoint) {
            string prefix = endpoint.Transport.ToString().ToLowerInvariant();
            return $"{prefix}@{endpoint}";
        }

        private static string BuildAnswerSignature(DnsResponse response) {
            if (response.Answers == null || response.Answers.Length == 0) {
                return "(no answers)";
            }

            string[] values = response.Answers
                .Select(answer => $"{answer.Name}|{answer.Type}|{answer.Data}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return string.Join(";", values);
        }
    }
}
