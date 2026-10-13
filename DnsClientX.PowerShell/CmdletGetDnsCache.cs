using System.Management.Automation;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Gets process-wide DNS response cache statistics.</para>
    /// <para type="description">Returns capacity, current entries and lifetime lookup counters shared by all cache-enabled DnsClientX clients in this PowerShell process. Clearing entries does not reset these counters.</para>
    /// </summary>
    /// <example><para>Inspect the response cache used by Resolve-Dns</para><code>Get-DnsCache</code></example>
    [Cmdlet(VerbsCommon.Get, "DnsCache")]
    [OutputType(typeof(DnsResponseCacheStatistics))]
    public sealed class CmdletGetDnsCache : PSCmdlet {
        /// <inheritdoc />
        protected override void ProcessRecord() {
            WriteObject(ClientX.GetResponseCacheStatistics());
        }
    }
}
