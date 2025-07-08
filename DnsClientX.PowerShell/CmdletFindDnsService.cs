using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Discovers services advertised via DNS Service Discovery.</para>
    /// <para type="description">Wraps <see cref="ClientX.DiscoverServices"/> to return services under a domain.</para>
    /// <example>
    ///   <para>Find HTTP services under example.com</para>
    ///   <code>Find-DnsService -Domain example.com</code>
    /// </example>
    /// </summary>
    [Cmdlet(VerbsCommon.Find, "DnsService")]
    public sealed class CmdletFindDnsService : AsyncPSCmdlet {
        /// <summary>Domain name to search for advertised services.</summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Domain { get; set; } = string.Empty;

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            using var client = new ClientX();
            var results = await client.DiscoverServices(Domain);
            WriteObject(results);
        }
    }
}
