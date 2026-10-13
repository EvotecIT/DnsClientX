using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Discovers services advertised via DNS Service Discovery.</para>
    /// <para type="description">Streams individual service instances under a domain, browses one service type, or lists advertised service types. An empty discovery produces no pipeline objects; use an array expression to collect zero or more results.</para>
    /// </summary>
    /// <example>
    ///   <para>Find services under a domain</para>
    ///   <code>Find-DnsService -Domain example.com</code>
    /// </example>
    /// <example>
    ///   <para>Filter for a specific service type</para>
    ///   <code>Find-DnsService -Domain example.com | Where-Object {$_.ServiceType -like '_http._tcp.*'}</code>
    /// </example>
    /// <example>
    ///   <para>List advertised service types through a specific resolver</para>
    ///   <code>Find-DnsService -Domain example.com -ServiceTypes -ResolverEndpoint 'tcp@192.0.2.53:53'</code>
    /// </example>
    /// <example>
    ///   <para>Browse only HTTP instances</para>
    ///   <code>Find-DnsService -Domain example.com -Service http -Protocol tcp</code>
    /// </example>
    [Cmdlet(VerbsCommon.Find, "DnsService", DefaultParameterSetName = "Discover")]
    [OutputType(typeof(DnsService), typeof(string))]
    public sealed class CmdletFindDnsService : DnsServiceCmdletBase {
        /// <summary>Domain name to search for advertised services.</summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("Name", "DnsName")]
        [ValidateNotNullOrEmpty]
        public string Domain { get; set; } = string.Empty;

        /// <summary><para type="description">List fully qualified service types from the DNS-SD meta-query.</para></summary>
        [Parameter(Mandatory = true, ParameterSetName = "Types")]
        [Alias("ListServiceTypes")]
        public SwitchParameter ServiceTypes { get; set; }

        /// <summary><para type="description">Service label to browse, such as http or _http.</para></summary>
        [Parameter(Mandatory = true, ParameterSetName = "Browse")]
        [ValidateNotNullOrEmpty]
        public string Service { get; set; } = string.Empty;

        /// <summary><para type="description">Service transport label, such as tcp or udp.</para></summary>
        [Parameter(ParameterSetName = "Browse")]
        [ValidateSet("tcp", "udp", "_tcp", "_udp")]
        public string Protocol { get; set; } = "tcp";

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            bool browse = ParameterSetName == "Browse";
            using var client = await CreateDiscoveryClientAsync().ConfigureAwait(false);
            if (ServiceTypes.IsPresent) {
                WriteObject(await client.DiscoverServiceTypesAsync(Domain, CancelToken).ConfigureAwait(false), true);
            } else if (browse) {
                WriteObject(await client.BrowseServicesAsync(Service, Protocol, Domain, CancelToken).ConfigureAwait(false), true);
            } else {
                await foreach (DnsService service in client.EnumerateServicesAsync(Domain, CancelToken).ConfigureAwait(false)) {
                    WriteObject(service);
                }
            }
        }
    }
}
