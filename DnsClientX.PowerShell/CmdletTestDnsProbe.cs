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
    /// Per-candidate DNS probe output for PowerShell consumers.
    /// </summary>
    public sealed class DnsProbeResult {
        /// <summary>
        /// Candidate label, such as a provider name or explicit transport endpoint.
        /// </summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>
        /// Resolver address observed for the candidate.
        /// </summary>
        public string Resolver { get; set; } = "none";

        /// <summary>
        /// Requested transport format for the candidate.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; set; }

        /// <summary>
        /// Actual or inferred transport observed for the candidate.
        /// </summary>
        public string Transport { get; set; } = "none";

        /// <summary>
        /// DNS response status or <c>NoResponse</c> when no response was received.
        /// </summary>
        public string Status { get; set; } = "NoResponse";

        /// <summary>
        /// Error details for failed probe attempts, or <c>none</c> when successful.
        /// </summary>
        public string Error { get; set; } = "none";

        /// <summary>
        /// Probe round-trip time in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Number of answers returned by the candidate.
        /// </summary>
        public int AnswerCount { get; set; }

        /// <summary>
        /// Indicates whether the probe succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Ranking position across all candidates for this probe run.
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// Indicates whether this candidate was the fastest successful responder.
        /// </summary>
        public bool IsFastestSuccess { get; set; }

        /// <summary>
        /// Indicates whether this candidate was the fastest responder within the leading consensus group.
        /// </summary>
        public bool IsFastestConsensus { get; set; }

        /// <summary>
        /// Indicates whether this candidate is recommended under the current probe policy.
        /// </summary>
        public bool IsRecommended { get; set; }

        /// <summary>
        /// Indicates whether the overall probe policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Explains why the overall probe policy failed, or <c>none</c> when it passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";
    }

    /// <summary>
    /// Run-level DNS probe summary for PowerShell consumers.
    /// </summary>
    public sealed class DnsProbeSummary {
        /// <summary>
        /// Domain name that was probed.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Record type that was probed.
        /// </summary>
        public DnsRecordType RecordType { get; set; }

        /// <summary>
        /// Per-query timeout in milliseconds used for the probe.
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Total candidate count in the run.
        /// </summary>
        public int CandidateCount { get; set; }

        /// <summary>
        /// Number of successful candidates in the run.
        /// </summary>
        public int SuccessfulCandidates { get; set; }

        /// <summary>
        /// Number of failed candidates in the run.
        /// </summary>
        public int FailedCandidates { get; set; }

        /// <summary>
        /// Successful candidate percentage across the run.
        /// </summary>
        public int SuccessPercent { get; set; }

        /// <summary>
        /// Indicates whether the overall probe policy passed.
        /// </summary>
        public bool PolicyPassed { get; set; }

        /// <summary>
        /// Explains why the overall probe policy failed, or <c>none</c> when it passed.
        /// </summary>
        public string PolicyReason { get; set; } = "none";

        /// <summary>
        /// Minimum successful probe count required by policy, if any.
        /// </summary>
        public int? RequiredMinSuccessCount { get; set; }

        /// <summary>
        /// Minimum successful probe percentage required by policy, if any.
        /// </summary>
        public int? RequiredMinSuccessPercent { get; set; }

        /// <summary>
        /// Minimum consensus percentage required by policy, if any.
        /// </summary>
        public int? RequiredMinConsensusPercent { get; set; }

        /// <summary>
        /// Indicates whether unanimous answer consensus was required.
        /// </summary>
        public bool RequireConsensus { get; set; }

        /// <summary>
        /// Number of distinct answer sets observed across successful candidates.
        /// </summary>
        public int DistinctAnswerSets { get; set; }

        /// <summary>
        /// Size of the leading consensus group.
        /// </summary>
        public int ConsensusCount { get; set; }

        /// <summary>
        /// Number of successful candidates considered for consensus.
        /// </summary>
        public int ConsensusTotal { get; set; }

        /// <summary>
        /// Consensus percentage for the leading answer group.
        /// </summary>
        public int ConsensusPercent { get; set; }

        /// <summary>
        /// Fastest successful candidate label.
        /// </summary>
        public string FastestSuccessTarget { get; set; } = "none";

        /// <summary>
        /// Resolver observed for the fastest successful candidate.
        /// </summary>
        public string FastestSuccessResolver { get; set; } = "none";

        /// <summary>
        /// Transport observed for the fastest successful candidate.
        /// </summary>
        public string FastestSuccessTransport { get; set; } = "none";

        /// <summary>
        /// Latency of the fastest successful candidate in milliseconds.
        /// </summary>
        public double FastestSuccessMs { get; set; }

        /// <summary>
        /// Fastest candidate label within the leading consensus group.
        /// </summary>
        public string FastestConsensusTarget { get; set; } = "none";

        /// <summary>
        /// Resolver observed for the fastest consensus candidate.
        /// </summary>
        public string FastestConsensusResolver { get; set; } = "none";

        /// <summary>
        /// Transport observed for the fastest consensus candidate.
        /// </summary>
        public string FastestConsensusTransport { get; set; } = "none";

        /// <summary>
        /// Latency of the fastest consensus candidate in milliseconds.
        /// </summary>
        public double FastestConsensusMs { get; set; }

        /// <summary>
        /// Indicates whether a recommendation is available.
        /// </summary>
        public bool RecommendationAvailable { get; set; }

        /// <summary>
        /// Recommended candidate label.
        /// </summary>
        public string RecommendedTarget { get; set; } = "none";

        /// <summary>
        /// Resolver observed for the recommended candidate.
        /// </summary>
        public string RecommendedResolver { get; set; } = "none";

        /// <summary>
        /// Transport observed for the recommended candidate.
        /// </summary>
        public string RecommendedTransport { get; set; } = "none";

        /// <summary>
        /// Latency of the recommended candidate in milliseconds.
        /// </summary>
        public double RecommendedAverageMs { get; set; }

        /// <summary>
        /// Source used to derive the recommendation.
        /// </summary>
        public string RecommendationSource { get; set; } = "none";

        /// <summary>
        /// Recommendation availability status.
        /// </summary>
        public string RecommendationStatus { get; set; } = "none";

        /// <summary>
        /// Reason why no recommendation was produced, or <c>none</c>.
        /// </summary>
        public string RecommendationReason { get; set; } = "none";
    }

    /// <summary>
    /// <para type="synopsis">Probes one built-in resolver profile or a custom resolver set and reports health, consensus, and recommendation data.</para>
    /// <para type="description">Runs a single DNS query against each candidate, highlights answer mismatches, applies optional success and consensus policy gates, and can persist the scored result set for later resolver selection and reuse.</para>
    /// <example>
    ///   <para>Probe the default system resolver profile for an A record</para>
    ///   <code>Test-DnsProbe -Name example.com</code>
    /// </example>
    /// <example>
    ///   <para>Probe the Cloudflare resolver family and return only the run-level summary</para>
    ///   <code>Test-DnsProbe -Name example.com -DnsProvider Cloudflare -SummaryOnly</code>
    /// </example>
    /// <example>
    ///   <para>Probe custom endpoints loaded from a file and require answer consensus</para>
    ///   <code>Test-DnsProbe -Name example.com -ResolverEndpointFile '.\resolvers.txt' -RequireConsensus -IncludeSummary</code>
    /// </example>
    /// <example>
    ///   <para>Probe custom endpoints from inline values and enforce success thresholds</para>
    ///   <code>Test-DnsProbe -Name example.com -ResolverEndpoint 'udp@1.1.1.1:53','tcp@9.9.9.9:53' -MinSuccessCount 2 -MinConsensusPercent 60</code>
    /// </example>
    /// <example>
    ///   <para>Probe modern transports with explicit endpoint strings</para>
    ///   <code>Test-DnsProbe -Name example.com -ResolverEndpoint 'doq@dns.quad9.net:853','doh3@https://dns.quad9.net/dns-query' -IncludeSummary</code>
    /// </example>
    /// <example>
    ///   <para>Reuse the recommended resolver from a saved snapshot as the single probe candidate</para>
    ///   <code>Test-DnsProbe -Name example.com -ResolverSelectionPath '.\resolver-score.json' -SummaryOnly</code>
    /// </example>
    /// <example>
    ///   <para>Probe resolvers and persist the scored snapshot for later selection</para>
    ///   <code>Test-DnsProbe -Name example.com -ResolverEndpointUrl 'https://example.test/resolvers.txt' -SavePath '.\resolver-probe.json' -IncludeSummary</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsDiagnostic.Test, "DnsProbe", DefaultParameterSetName = "DnsProvider")]
    [OutputType(typeof(DnsProbeResult), typeof(DnsProbeSummary))]
    public sealed class CmdletTestDnsProbe : AsyncPSCmdlet {
        /// <summary>
        /// Domain name to probe.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ResolverSelection")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Record type to probe.
        /// </summary>
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, Position = 1, ParameterSetName = "ResolverSelection")]
        public DnsRecordType Type { get; set; } = DnsRecordType.A;

        /// <summary>
        /// Built-in resolver profile to probe.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        public DnsEndpoint DnsProvider { get; set; } = DnsEndpoint.System;

        /// <summary>
        /// Explicit resolver endpoints to probe.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpoint { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Files containing resolver endpoints to probe.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpointFile { get; set; } = Array.Empty<string>();

        /// <summary>
        /// HTTP or HTTPS URLs exposing resolver endpoints to probe.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        public string[] ResolverEndpointUrl { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Path to a saved resolver score snapshot whose recommended resolver should be reused as the single probe candidate.
        /// </summary>
        [Parameter(Mandatory = true, ParameterSetName = "ResolverSelection")]
        public string ResolverSelectionPath { get; set; } = string.Empty;

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
        /// Require unanimous answer consensus among successful candidates.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public SwitchParameter RequireConsensus { get; set; }

        /// <summary>
        /// Require the leading answer group to reach at least this consensus percentage.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int? MinConsensusPercent { get; set; }

        /// <summary>
        /// Require at least this many successful probe candidates.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int? MinSuccessCount { get; set; }

        /// <summary>
        /// Require at least this successful probe percentage across all candidates.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public int? MinSuccessPercent { get; set; }

        /// <summary>
        /// Emits a run-level summary object after the per-candidate probe rows.
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
        /// Optional path where the probe score snapshot should be saved for later selection and reuse.
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "DnsProvider")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverEndpoint")]
        [Parameter(Mandatory = false, ParameterSetName = "ResolverSelection")]
        public string SavePath { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            ValidateParameters();

            ResolverExecutionTargetSource targetSource = CreateTargetSource();
            ResolverExecutionTarget[] candidates = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, CancelToken).ConfigureAwait(false);
            if (candidates.Length == 0) {
                WriteWarning("No probe candidates were produced from the supplied input.");
                return;
            }

            WriteVerbose($"Probing {candidates.Length} candidate(s) for {Name} {Type}.");

            ResolverProbeReport report = await RunProbeAsync(candidates, CancelToken).ConfigureAwait(false);
            DnsProbeResult[] results = report.Results.Select(ToDnsProbeResult).ToArray();
            DnsProbeSummary summary = ToDnsProbeSummary(report.Summary);

            if (!report.Evaluation.PolicyPassed) {
                WriteWarning($"Probe policy failed: {report.Evaluation.PolicyReason}");
            } else {
                WriteVerbose("Probe policy passed.");
            }

            if (!string.IsNullOrWhiteSpace(SavePath)) {
                ResolverScoreStore.Save(SavePath, report.Snapshot);
                WriteVerbose($"Saved resolver probe snapshot to {SavePath}.");
            }

            if (!SummaryOnly.IsPresent) {
                WriteObject(results, true);
            }

            if (IncludeSummary.IsPresent) {
                WriteObject(summary);
            }
        }

        private void ValidateParameters() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new PSArgumentException("A non-empty domain name is required.", nameof(Name));
            }

            if (TimeOut < 1) {
                throw new PSArgumentOutOfRangeException(nameof(TimeOut), TimeOut, "TimeOut must be at least 1.");
            }

            if (MinConsensusPercent.HasValue && (MinConsensusPercent.Value < 1 || MinConsensusPercent.Value > 100)) {
                throw new PSArgumentOutOfRangeException(nameof(MinConsensusPercent), MinConsensusPercent.Value, "MinConsensusPercent must be between 1 and 100.");
            }

            if (MinSuccessCount.HasValue && MinSuccessCount.Value < 1) {
                throw new PSArgumentOutOfRangeException(nameof(MinSuccessCount), MinSuccessCount.Value, "MinSuccessCount must be at least 1.");
            }

            if (MinSuccessPercent.HasValue && (MinSuccessPercent.Value < 1 || MinSuccessPercent.Value > 100)) {
                throw new PSArgumentOutOfRangeException(nameof(MinSuccessPercent), MinSuccessPercent.Value, "MinSuccessPercent must be between 1 and 100.");
            }

            if (SummaryOnly.IsPresent) {
                IncludeSummary = true;
            }

            if (ParameterSetName == "DnsProvider" && DnsProvider == DnsEndpoint.Custom) {
                throw new PSArgumentException("DnsEndpoint.Custom is not valid with -DnsProvider. Use -ResolverEndpoint instead.", nameof(DnsProvider));
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
                ProbeProfile = DnsProvider
            };
        }

        private ResolverProbePolicy CreateProbePolicy() {
            return new ResolverProbePolicy {
                MinSuccessCount = MinSuccessCount,
                MinSuccessPercent = MinSuccessPercent,
                MinConsensusPercent = MinConsensusPercent,
                RequireConsensus = RequireConsensus.IsPresent
            };
        }

        private async Task<ResolverProbeReport> RunProbeAsync(IReadOnlyList<ResolverExecutionTarget> candidates, CancellationToken cancellationToken) {
            ResolverProbeReport report = await ResolverProbeWorkflow.RunAsync(
                candidates,
                Name,
                Type,
                TimeOut,
                CreateQueryRunOptions(),
                CreateProbePolicy(),
                progress: (completed, total) => {
                    int percent = total == 0
                        ? 100
                        : (int)Math.Round((double)completed * 100 / total, MidpointRounding.AwayFromZero);
                    WriteProgress(new ProgressRecord(2, "Testing DNS probe candidates", $"{completed}/{total} candidates completed") {
                        PercentComplete = percent
                    });
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            WriteProgress(new ProgressRecord(2, "Testing DNS probe candidates", "Completed") {
                PercentComplete = 100,
                RecordType = ProgressRecordType.Completed
            });

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

        private static DnsProbeResult ToDnsProbeResult(ResolverProbeReportResult result) {
            return new DnsProbeResult {
                Target = result.Target,
                Resolver = result.Resolver,
                RequestFormat = result.RequestFormat,
                Transport = result.Transport,
                Status = result.Status,
                Error = result.Error,
                ElapsedMs = result.ElapsedMs,
                AnswerCount = result.AnswerCount,
                Succeeded = result.Succeeded,
                Rank = result.Rank,
                IsFastestSuccess = result.IsFastestSuccess,
                IsFastestConsensus = result.IsFastestConsensus,
                IsRecommended = result.IsRecommended,
                PolicyPassed = result.PolicyPassed,
                PolicyReason = result.PolicyReason
            };
        }

        private static DnsProbeSummary ToDnsProbeSummary(ResolverProbeReportSummary summary) {
            return new DnsProbeSummary {
                Name = summary.Name,
                RecordType = summary.RecordType,
                TimeoutMs = summary.TimeoutMs,
                CandidateCount = summary.CandidateCount,
                SuccessfulCandidates = summary.SuccessfulCandidates,
                FailedCandidates = summary.FailedCandidates,
                SuccessPercent = summary.SuccessPercent,
                PolicyPassed = summary.PolicyPassed,
                PolicyReason = summary.PolicyReason,
                RequiredMinSuccessCount = summary.RequiredMinSuccessCount,
                RequiredMinSuccessPercent = summary.RequiredMinSuccessPercent,
                RequiredMinConsensusPercent = summary.RequiredMinConsensusPercent,
                RequireConsensus = summary.RequireConsensus,
                DistinctAnswerSets = summary.DistinctAnswerSets,
                ConsensusCount = summary.ConsensusCount,
                ConsensusTotal = summary.ConsensusTotal,
                ConsensusPercent = summary.ConsensusPercent,
                FastestSuccessTarget = summary.FastestSuccessTarget,
                FastestSuccessResolver = summary.FastestSuccessResolver,
                FastestSuccessTransport = summary.FastestSuccessTransport,
                FastestSuccessMs = summary.FastestSuccessMs,
                FastestConsensusTarget = summary.FastestConsensusTarget,
                FastestConsensusResolver = summary.FastestConsensusResolver,
                FastestConsensusTransport = summary.FastestConsensusTransport,
                FastestConsensusMs = summary.FastestConsensusMs,
                RecommendationAvailable = summary.RecommendationAvailable,
                RecommendedTarget = summary.RecommendedTarget,
                RecommendedResolver = summary.RecommendedResolver,
                RecommendedTransport = summary.RecommendedTransport,
                RecommendedAverageMs = summary.RecommendedAverageMs,
                RecommendationSource = summary.RecommendationSource,
                RecommendationStatus = summary.RecommendationStatus,
                RecommendationReason = summary.RecommendationReason
            };
        }

    }
}
