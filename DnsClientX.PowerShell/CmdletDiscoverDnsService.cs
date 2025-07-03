using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Retrieves services advertised via DNS Service Discovery.</para>
    /// <para type="description">Queries the <c>_services._dns-sd._udp</c> tree for the specified domain and returns SRV/TXT data describing each advertised service.</para>
    /// <example>
    ///   <para>Discover HTTP services under example.com</para>
    ///   <code>Get-DnsService -Domain example.com</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "DnsService")]
    public sealed class CmdletDiscoverDnsService : AsyncPSCmdlet {
        /// <summary>
        /// <para type="description">Domain name to search for advertised services.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Executes the cmdlet logic asynchronously.
        /// </summary>
        protected override async Task ProcessRecordAsync() {
            using var client = new ClientX();
            var results = await client.DiscoverServices(Domain);
            WriteObject(results, true);
        }
    }
}
