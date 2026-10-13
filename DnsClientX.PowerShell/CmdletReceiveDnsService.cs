using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell;

/// <summary>
/// <para type="synopsis">Continuously reports additions, changes, goodbyes and expirations of multicast DNS services.</para>
/// <para type="description">Uses a bounded persistent RFC 6762 multicast browse session. Stopping the pipeline
/// cancels and drains its receiver. Each output is an independent service snapshot with its observed change.</para>
/// </summary>
/// <example>
/// <para>Watch HTTP services on the local multicast network.</para>
/// <code>Receive-DnsService -Service http -Protocol tcp | Select-Object Change,Service,Addresses,ObservedAt</code>
/// </example>
[Alias("Watch-DnsService")]
[Cmdlet(VerbsCommunications.Receive, "DnsService")]
[OutputType(typeof(DnsServiceBrowseEvent))]
public sealed class CmdletReceiveDnsService : AsyncPSCmdlet {
    /// <summary><para type="description">Service label, with or without its leading underscore.</para></summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Service { get; set; } = string.Empty;

    /// <summary><para type="description">Transport label, with or without its leading underscore.</para></summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Protocol { get; set; } = string.Empty;

    /// <summary><para type="description">Domain to browse. The default is local.</para></summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string Domain { get; set; } = "local";

    /// <summary><para type="description">Optional multicast configuration with the desired interface and port.</para></summary>
    [Parameter]
    public Configuration? Configuration { get; set; }

    /// <summary><para type="description">Core cache, instance and output-buffer resource bounds.</para></summary>
    [Parameter]
    public DnsServiceBrowseOptions? Options { get; set; }

    /// <inheritdoc/>
    protected override async Task ProcessRecordAsync() {
        Configuration configuration = Configuration ?? new Configuration("224.0.0.251", DnsRequestFormat.Multicast);
        using var client = ResolverExecutionClientFactory.CreateConfiguredClient(configuration);
        await foreach (DnsServiceBrowseEvent change in client.BrowseServicesContinuousAsync(Service, Protocol, Domain, Options, CancelToken).ConfigureAwait(false)) {
            WriteObject(change);
        }
    }
}
