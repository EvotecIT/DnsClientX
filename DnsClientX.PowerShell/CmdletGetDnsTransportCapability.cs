using System.Management.Automation;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Returns runtime transport support information for the DnsClientX core transport surface.</para>
    /// <para type="description">Reports which core transports are available on the current runtime, including modern DoH3 and DoQ support on .NET 8+.</para>
    /// <example>
    ///   <para>List the full core transport capability report</para>
    ///   <code>Get-DnsTransportCapability</code>
    /// </example>
    /// <example>
    ///   <para>Show only the runtime-gated modern transport entries</para>
    ///   <code>Get-DnsTransportCapability -ModernOnly</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "DnsTransportCapability")]
    [OutputType(typeof(DnsTransportCapabilityInfo))]
    public sealed class CmdletGetDnsTransportCapability : AsyncPSCmdlet {
        /// <summary>
        /// Emits only modern runtime-gated transports such as DoH3 and DoQ.
        /// </summary>
        [Parameter(Mandatory = false)]
        public SwitchParameter ModernOnly { get; set; }

        /// <inheritdoc />
        protected override Task ProcessRecordAsync() {
            WriteObject(DnsTransportCapabilities.GetCapabilityReport(ModernOnly.IsPresent), true);
            return Task.CompletedTask;
        }
    }
}
