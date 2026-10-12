using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Clears process-wide cached DNS responses.</para>
    /// <para type="description">Clears all response entries, or entries for one queried name and optional type across all resolver policies. Returns the number removed. This affects every cache-enabled DnsClientX client in this process; active queries may repopulate the cache. FastestWins endpoint choices are managed by Clear-DnsMultiResolverCache.</para>
    /// </summary>
    /// <example><para>Clear all cached responses</para><code>Clear-DnsCache</code></example>
    /// <example><para>Remove cached A responses for one name</para><code>Clear-DnsCache -Name example.com -Type A</code></example>
    /// <example><para>Preview selective invalidation</para><code>Clear-DnsCache -Name example.com -WhatIf</code></example>
    [Cmdlet(VerbsCommon.Clear, "DnsCache", SupportsShouldProcess = true, DefaultParameterSetName = "All")]
    [OutputType(typeof(int))]
    public sealed class CmdletClearDnsCache : AsyncPSCmdlet {
        /// <summary><para type="description">Original queried name to invalidate, including the expanded system-search name when applicable. Names normalize case, Unicode and the trailing root dot. Alias targets do not invalidate a different original query.</para></summary>
        [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Name", ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("Domain", "DnsName")]
        [ValidateNotNullOrEmpty]
        public string Name { get; set; } = string.Empty;

        /// <summary><para type="description">Only remove this query type. Omit to remove every cached type for Name.</para></summary>
        [Parameter(Position = 1, ParameterSetName = "Name")]
        public DnsRecordType? Type { get; set; }

        /// <inheritdoc />
        protected override Task ProcessRecordAsync() {
            bool selective = ParameterSetName == "Name";
            string target = selective ? $"{Name} ({(Type.HasValue ? Type.Value.ToString() : "all types")}) across all resolver policies" : "all cached DNS responses";
            if (ShouldProcess(target, "Clear process-wide DNS response cache entries")) {
                WriteObject(selective ? ClientX.InvalidateResponseCache(Name, Type) : ClientX.ClearResponseCache());
            }
            return Task.CompletedTask;
        }
    }
}
