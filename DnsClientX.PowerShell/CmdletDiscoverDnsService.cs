using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell {
    /// <summary>
    /// <para type="synopsis">Retrieves services advertised via DNS Service Discovery.</para>
    /// <para type="description">Streams individual DNS-SD instances under a domain, browses one service type, or resolves SRV targets in connection-attempt order. Empty discovery produces no pipeline objects; use an array expression to collect zero or more results.</para>
    /// </summary>
    /// <example>
    ///   <para>Discover services under a domain</para>
    ///   <code>Get-DnsService -Domain example.com</code>
    /// </example>
    /// <example>
    ///   <para>Pipe results to select useful fields</para>
    ///   <code>Get-DnsService -Domain example.com | Select-Object ServiceName,Target,Port</code>
    /// </example>
    /// <example>
    ///   <para>Browse HTTP service instances through an explicit resolver</para>
    ///   <code>Get-DnsService -Domain example.com -Service http -ResolverEndpoint 'https://dns.google/dns-query'</code>
    /// </example>
    /// <example>
    ///   <para>Resolve LDAP targets and addresses in connection-attempt order</para>
    ///   <code>Get-DnsService -Domain example.com -Service ldap -Resolve -ResolveHosts</code>
    /// </example>
    [Cmdlet(VerbsCommon.Get, "DnsService", DefaultParameterSetName = "Discover")]
    [OutputType(typeof(DnsService), typeof(DnsSrvRecord))]
    public sealed class CmdletDiscoverDnsService : DnsServiceCmdletBase {
        /// <summary>
        /// <para type="description">Domain name to search for advertised services.</para>
        /// </summary>
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [Alias("Name", "DnsName")]
        [ValidateNotNullOrEmpty]
        public string Domain { get; set; } = string.Empty;

        /// <summary><para type="description">Service label to browse or resolve, such as http or _http.</para></summary>
        [Parameter(Mandatory = true, ParameterSetName = "Browse")]
        [Parameter(Mandatory = true, ParameterSetName = "Resolve")]
        [ValidateNotNullOrEmpty]
        public string Service { get; set; } = string.Empty;

        /// <summary><para type="description">Service transport label, such as tcp or udp.</para></summary>
        [Parameter(ParameterSetName = "Browse")]
        [Parameter(ParameterSetName = "Resolve")]
        [ValidateSet("tcp", "udp", "_tcp", "_udp")]
        public string Protocol { get; set; } = "tcp";

        /// <summary><para type="description">Resolve SRV targets in RFC 2782 connection-attempt order instead of browsing DNS-SD instances.</para></summary>
        [Parameter(Mandatory = true, ParameterSetName = "Resolve")]
        public SwitchParameter Resolve { get; set; }

        /// <summary><para type="description">With Resolve, include A and AAAA addresses for each target host.</para></summary>
        [Parameter(ParameterSetName = "Resolve")]
        public SwitchParameter ResolveHosts { get; set; }

        /// <inheritdoc />
        protected override async Task ProcessRecordAsync() {
            bool browse = ParameterSetName == "Browse";
            using var client = await CreateDiscoveryClientAsync().ConfigureAwait(false);
            if (Resolve.IsPresent) {
                WriteObject(await client.ResolveServiceAsync(Service, Protocol, Domain, ResolveHosts.IsPresent, CancelToken).ConfigureAwait(false), true);
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
