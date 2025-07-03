using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// Discovers DNS-SD services for a given domain.
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "DnsService")]
    [OutputType(typeof(DnsServiceDiscovery))]
    public sealed class CmdletDiscoverDnsService : AsyncPSCmdlet {
        /// <summary>
        /// Domain to discover services for.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Domain { get; set; } = string.Empty;

        protected override async Task ProcessRecordAsync() {
            var client = new ClientX();
            var results = await client.DiscoverServices(Domain);
            foreach (var sd in results) {
                WriteObject(sd);
            }
        }
    }
}
