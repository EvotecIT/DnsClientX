using System;
using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell;

/// <summary>
/// <para type="synopsis">Sends DNS UPDATE messages to a server.</para>
/// <para type="description">Adds or removes records in a zone using RFC 2136 over TCP.</para>
/// <example>
///   <para>Add an A record</para>
///   <code>Invoke-DnsUpdate -Zone example.com -Server 127.0.0.1 -Name www -Type A -Data 1.2.3.4 -Ttl 300</code>
/// </example>
/// <example>
///   <para>Delete an existing record</para>
///   <code>Invoke-DnsUpdate -Zone example.com -Server 127.0.0.1 -Name www -Type A -Delete</code>
/// </example>
/// </summary>
[Cmdlet(VerbsLifecycle.Invoke, "DnsUpdate")]
public sealed class CmdletDnsUpdate : AsyncPSCmdlet {
    /// <summary>Zone to update.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    public string Zone { get; set; } = string.Empty;

    /// <summary>DNS server to send the update to.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("ServerName")]
    public string Server { get; set; } = string.Empty;

    /// <summary>Port number to use. Defaults to 53.</summary>
    [Parameter(Mandatory = false)]
    public int Port { get; set; } = 53;

    /// <summary>Record name.</summary>
    [Parameter(Mandatory = true, Position = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of record.</summary>
    [Parameter(Mandatory = true, Position = 3)]
    public DnsRecordType Type { get; set; }

    /// <summary>Record data used when adding a record.</summary>
    [Parameter(Position = 4)]
    public string Data { get; set; } = string.Empty;

    /// <summary>TTL for the new record. Defaults to 300 seconds.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int Ttl { get; set; } = 300;

    /// <summary>If specified, the record is removed instead of added.</summary>
    [Parameter]
    public SwitchParameter Delete;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        using var client = new ClientX(Server, DnsRequestFormat.DnsOverTCP) { EndpointConfiguration = { Port = Port } };
        DnsResponse result = Delete.IsPresent
            ? await client.DeleteRecordAsync(Zone, Name, Type)
            : await client.UpdateRecordAsync(Zone, Name, Type, Data, Ttl);
        WriteObject(result);
    }
}

