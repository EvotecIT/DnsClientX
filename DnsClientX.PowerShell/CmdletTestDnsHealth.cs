using System;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Runs a repeatable DNS resolver health profile.</para>
    /// <para type="description">Runs the profile's DNS query catalog and policy against its configured resolvers. IncludeBenchmark measures the same catalog. Returns the complete report; resolver agreement alone does not establish authoritative correctness.</para>
    /// </summary>
    /// <example><para>Run a saved profile and fail an automation run when its policy fails</para><code>Test-DnsHealth -Path './dns-health.json' -FailOnPolicyViolation -ErrorAction Stop</code></example>
    /// <example><para>Include performance measurements for the same profile</para><code>Test-DnsHealth -Path './dns-health.json' -IncludeBenchmark</code></example>
    [Cmdlet(VerbsDiagnostic.Test, "DnsHealth", DefaultParameterSetName = "Path")]
    [OutputType(typeof(ResolverHealthReport))]
    public sealed class CmdletTestDnsHealth : AsyncPSCmdlet {
        /// <summary><para type="description">Local JSON health profile path.</para></summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path")]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; } = string.Empty;

        /// <summary><para type="description">Typed health profile supplied directly or through the pipeline.</para></summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Profile", ValueFromPipeline = true)]
        [ValidateNotNull]
        public ResolverHealthProfile? Profile { get; set; }

        /// <summary><para type="description">Benchmark the same DNS names, types and resolver targets after the health queries.</para></summary>
        [Parameter]
        public SwitchParameter IncludeBenchmark { get; set; }

        /// <summary><para type="description">Write a PolicyViolation error on failed health gates. Use ErrorAction Stop for enforcement; the default only warns.</para></summary>
        [Parameter]
        public SwitchParameter FailOnPolicyViolation { get; set; }

        /// <summary><para type="description">Local optional DNSSEC verifier DLL with dependencies beside it.</para></summary>
        [Parameter]
        public string? DnsSecVerifierPath { get; set; }

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            ResolverHealthProfile profile = Profile ?? ResolverHealthProfileStore.Load(SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path));
            IDnsSecSignatureVerifier? verifier = DnsSecProviderPath.Load(this, DnsSecVerifierPath);
            ResolverHealthReport report = await ResolverHealthWorkflow.RunAsync(profile, IncludeBenchmark.IsPresent, verifier,
                cancellationToken: CancelToken).ConfigureAwait(false);
            WriteObject(report);
            if (!report.PolicyPassed) {
                int failedQueries = report.Queries.Count(query => !query.Evaluation.PolicyPassed || query.Evaluation.SuccessfulCandidates == 0);
                string message = $"DNS health profile '{report.ProfileName}' failed: {failedQueries} query gate(s) failed. Inspect Queries and Benchmark for details.";
                if (FailOnPolicyViolation.IsPresent) {
                    WriteError(new ErrorRecord(new InvalidOperationException(message), "DnsHealthPolicyViolation", ErrorCategory.InvalidResult, report));
                } else {
                    WriteWarning(message);
                }
            }
        }
    }
}
