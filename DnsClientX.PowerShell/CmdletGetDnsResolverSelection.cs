using System.Management.Automation;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Loads a saved resolver score snapshot and returns the recommended resolver selection.</para>
    /// <para type="description">Reads a persisted resolver score snapshot, applies the shared recommendation logic, and returns either the structured selection object or the raw target string for automation use.</para>
    /// <example>
    ///   <para>Get the recommended resolver selection as an object</para>
    ///   <code>Get-DnsResolverSelection -Path '.\resolver-score.json'</code>
    /// </example>
    /// <example>
    ///   <para>Get only the raw selected target string for scripting</para>
    ///   <code>Get-DnsResolverSelection -Path '.\resolver-score.json' -AsString</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "DnsResolverSelection")]
    [OutputType(typeof(ResolverSelectionResult), typeof(string))]
    public sealed class CmdletGetDnsResolverSelection : AsyncPSCmdlet {
        /// <summary>
        /// Path to the saved resolver score snapshot.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Emits only the raw selected target string instead of the structured selection object.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter AsString { get; set; }

        /// <inheritdoc />
        protected override Task ProcessRecordAsync() {
            ResolverScoreSnapshot snapshot = ResolverScoreStore.Load(Path);
            if (!ResolverScoreSelector.TrySelectRecommended(snapshot, out ResolverSelectionResult? selection, out string? error) || selection == null) {
                throw new PSArgumentException(error ?? "No recommended resolver could be selected from the snapshot.", nameof(Path));
            }

            if (AsString.IsPresent) {
                WriteObject(selection.Target);
            } else {
                WriteObject(selection);
            }

            return Task.CompletedTask;
        }
    }
}
