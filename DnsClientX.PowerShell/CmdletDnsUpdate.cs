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
/// <example>
///   <para>Delete one value while preserving the rest of the RRset</para>
///   <code>Invoke-DnsUpdate -Zone example.com -Server 127.0.0.1 -Name www -Type A -Data 192.0.2.10 -DeleteValue</code>
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

    /// <summary>If specified, only the exact value supplied through <see cref="Data"/> is removed.</summary>
    [Parameter]
    public SwitchParameter DeleteValue;

    /// <summary>Optional typed TSIG key used to authenticate the update and its response.</summary>
    [Parameter]
    public TsigKey? TsigKey { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (Delete.IsPresent && DeleteValue.IsPresent) {
            throw new PSArgumentException("-Delete and -DeleteValue cannot be used together.");
        }
        if (DeleteValue.IsPresent && string.IsNullOrWhiteSpace(Data)) {
            throw new PSArgumentException("-DeleteValue requires -Data with the exact record value to remove.");
        }

        var target = new ResolverExecutionTarget {
            DisplayName = $"tcp@{Server}:{Port}",
            ExplicitEndpoint = new DnsResolverEndpoint {
                Transport = Transport.Tcp,
                Host = Server,
                Port = Port
            }
        };

        var options = new ResolverExecutionClientOptions { TsigKey = TsigKey };

        DnsResponse result = Delete.IsPresent
            ? await ResolverUpdateWorkflow.DeleteAsync(target, Zone, Name, Type, options, CancelToken).ConfigureAwait(false)
            : DeleteValue.IsPresent
                ? await ResolverUpdateWorkflow.DeleteValueAsync(target, Zone, Name, Type, Data, options, CancelToken).ConfigureAwait(false)
                : await ResolverUpdateWorkflow.UpdateAsync(target, Zone, Name, Type, Data, Ttl, options, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }
}

