using System.Management.Automation;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Compares resolver health and answers between saved DNS snapshots.</para>
    /// <para type="description">Reports candidate and answer changes, health regressions, and incompatible query scopes. Optional authoritative responses provide comparison context; a majority answer is not proof of authoritative correctness.</para>
    /// </summary>
    /// <example><para>Compare two saved probe or benchmark snapshots</para><code>Compare-DnsResolverSnapshot -BaselinePath './before.json' -CurrentPath './after.json'</code></example>
    [Cmdlet(VerbsData.Compare, "DnsResolverSnapshot", DefaultParameterSetName = "Path")]
    [OutputType(typeof(ResolverScoreComparison))]
    public sealed class CmdletCompareDnsResolverSnapshot : PSCmdlet {
        /// <summary><para type="description">Baseline resolver score snapshot JSON path.</para></summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Path")]
        [ValidateNotNullOrEmpty]
        public string BaselinePath { get; set; } = string.Empty;

        /// <summary><para type="description">Current resolver score snapshot JSON path.</para></summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Path")]
        [ValidateNotNullOrEmpty]
        public string CurrentPath { get; set; } = string.Empty;

        /// <summary><para type="description">Baseline typed resolver score snapshot.</para></summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Snapshot")]
        [ValidateNotNull]
        public ResolverScoreSnapshot? Baseline { get; set; }

        /// <summary><para type="description">Current typed resolver score snapshot, supplied directly or through the pipeline.</para></summary>
        [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Snapshot", ValueFromPipeline = true)]
        [ValidateNotNull]
        public ResolverScoreSnapshot? Current { get; set; }

        /// <summary><para type="description">Caller-supplied authoritative responses for name/type comparison context. These must assert the AA flag; response origin remains the caller's responsibility.</para></summary>
        [Parameter]
        public ResolverAnswerReference[]? AuthoritativeReference { get; set; }

        /// <inheritdoc />
        protected override void ProcessRecord() {
            ResolverScoreSnapshot baseline = Baseline ?? ResolverScoreStore.Load(SessionState.Path.GetUnresolvedProviderPathFromPSPath(BaselinePath));
            ResolverScoreSnapshot current = Current ?? ResolverScoreStore.Load(SessionState.Path.GetUnresolvedProviderPathFromPSPath(CurrentPath));
            WriteObject(ResolverScoreComparer.Compare(baseline, current, AuthoritativeReference));
        }
    }
}
