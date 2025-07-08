using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell;

/// <summary>
/// <para type="synopsis">Performs a DNS zone transfer (AXFR) for a given zone.</para>
/// <para type="description">Retrieves all records for the specified zone using TCP based zone transfer.</para>
/// <example>
///   <para>Transfer zone from a DNS server</para>
///   <code>Get-DnsZoneTransfer -Zone example.com -Server 127.0.0.1 -Port 5353</code>
/// </example>
/// </summary>
[Alias("Get-DnsZoneTransfer")]
[Cmdlet(VerbsCommon.Get, "DnsZone")]
public sealed class CmdletDnsZoneTransfer : AsyncPSCmdlet {
    /// <summary>The zone to transfer.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Zone { get; set; } = string.Empty;

    /// <summary>DNS server to query.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("ServerName")]
    public string Server { get; set; } = string.Empty;

    /// <summary>Port number to use. Defaults to 53.</summary>
    [Parameter(Mandatory = false)]
    public int Port { get; set; } = 53;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using var client = new ClientX(Server, DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = Port } };
        await foreach (var rrset in client.ZoneTransferStreamAsync(Zone, cancellationToken: CancelToken)) {
            WriteObject(rrset);
        }
    }
}
