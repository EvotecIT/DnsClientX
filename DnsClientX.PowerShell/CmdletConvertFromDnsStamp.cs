using System.Management.Automation;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Parses a DNS stamp into a resolver endpoint description.</para>
    /// <para type="description">Converts a supported sdns:// DNS stamp into the same endpoint model used by DnsClientX resolver workflows. This command does not perform a DNS query.</para>
    /// <example>
    ///   <para>Parse a DNS-over-HTTPS stamp</para>
    ///   <code>ConvertFrom-DnsStamp -Stamp 'sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk'</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsData.ConvertFrom, "DnsStamp")]
    [OutputType(typeof(DnsStampInfo))]
    public sealed class CmdletConvertFromDnsStamp : AsyncPSCmdlet {
        /// <summary>
        /// DNS stamp to parse.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string Stamp { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override Task ProcessRecordAsync() {
            WriteObject(DnsStamp.Describe(Stamp));
            return Task.CompletedTask;
        }
    }
}
