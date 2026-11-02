using System.Management.Automation;
using System.Threading.Tasks;
using DnsClientX;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Parses a DNS stamp into a resolver endpoint description.</para>
    /// <para type="description">Converts a supported sdns:// DNS stamp into a resolver endpoint description or a role-specific DNSCrypt/ODoH identity. This command does not load a provider or perform a DNS query.</para>
    /// </summary>
    /// <example>
    ///   <para>Parse a DNS-over-HTTPS stamp</para>
    ///   <code>ConvertFrom-DnsStamp -Stamp 'sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk'</code>
    /// </example>
    [Cmdlet(VerbsData.ConvertFrom, "DnsStamp")]
    [OutputType(typeof(DnsStampInfo))]
    [OutputType(typeof(DnsPrivacyStamp))]
    public sealed class CmdletConvertFromDnsStamp : AsyncPSCmdlet {
        /// <summary>
        /// DNS stamp to parse.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
        [ValidateNotNullOrEmpty]
        public string Stamp { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override Task ProcessRecordAsync() {
            WriteObject(DnsStamp.IsPrivacyStamp(Stamp) ? (object)DnsStamp.ParsePrivacy(Stamp) : DnsStamp.Describe(Stamp));
            return Task.CompletedTask;
        }
    }
}
