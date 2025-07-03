using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    [Cmdlet(VerbsCommon.Get, "DnsService")]
    public sealed class CmdletDiscoverDnsService : AsyncPSCmdlet {
        [Parameter(Mandatory = true, Position = 0)]
        public string Domain { get; set; } = string.Empty;

        protected override async Task ProcessRecordAsync() {
            using var client = new ClientX();
            var results = await client.DiscoverServices(Domain);
            WriteObject(results, true);
        }
    }
}
