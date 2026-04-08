using System.Management.Automation;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell;

/// <summary>
/// <para type="synopsis">Performs a DNS zone transfer (AXFR) for a given zone.</para>
/// <para type="description">Retrieves all records for the specified zone using TCP based zone transfer.</para>
/// <example>
///   <para>Transfer a zone (default port 53)</para>
///   <code>Get-DnsZone -Zone example.com -Server 127.0.0.1</code>
/// </example>
/// <example>
///   <para>Transfer a zone from a custom port</para>
///   <code>Get-DnsZone -Zone example.com -Server 127.0.0.1 -Port 5353</code>
/// </example>
/// <example>
///   <para>Discover authoritative servers and transfer from the first one that allows AXFR</para>
///   <code>Get-DnsZone -Zone example.com -Recursive</code>
/// </example>
/// </summary>
[Alias("Get-DnsZoneTransfer")]
[Cmdlet(VerbsCommon.Get, "DnsZone", DefaultParameterSetName = "ExplicitServer")]
public sealed class CmdletDnsZoneTransfer : AsyncPSCmdlet {
    /// <summary>The zone to transfer.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "ExplicitServer")]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Recursive")]
    public string Zone { get; set; } = string.Empty;

    /// <summary>DNS server to query.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "ExplicitServer")]
    [Parameter(Mandatory = false, Position = 1, ParameterSetName = "Recursive")]
    [Alias("ServerName")]
    public string Server { get; set; } = string.Empty;

    /// <summary>Port number to use. Defaults to 53.</summary>
    [Parameter(Mandatory = false)]
    public int Port { get; set; } = 53;

    /// <summary>Discover authoritative servers first and attempt AXFR against them in order.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Recursive")]
    public SwitchParameter Recursive { get; set; }

    /// <summary>Resolver profile used to discover authoritative servers when <see cref="Recursive"/> is specified and no explicit discovery server is provided.</summary>
    [Parameter(Mandatory = false, ParameterSetName = "Recursive")]
    public DnsEndpoint DnsProvider { get; set; } = DnsEndpoint.System;

    /// <summary>Emit the recursive transfer summary object before the transferred RRsets.</summary>
    [Parameter(Mandatory = false, ParameterSetName = "Recursive")]
    public SwitchParameter IncludeTransferSummary { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (Recursive.IsPresent) {
            ResolverExecutionTarget target = string.IsNullOrWhiteSpace(Server)
                ? new ResolverExecutionTarget {
                    DisplayName = DnsProvider.ToString(),
                    BuiltInEndpoint = DnsProvider
                }
                : new ResolverExecutionTarget {
                    DisplayName = $"udp@{Server}:{Port}",
                    ExplicitEndpoint = new DnsResolverEndpoint {
                        Transport = Transport.Udp,
                        Host = Server,
                        Port = Port
                    }
                };

            RecursiveZoneTransferResult result = await ResolverZoneTransferWorkflow.RunRecursiveAsync(target, Zone, port: Port, cancellationToken: CancelToken).ConfigureAwait(false);
            WriteVerbose($"Recursive AXFR succeeded for {result.Zone} via {result.SelectedServer} (authority {result.SelectedAuthority}).");
            WriteVerbose($"Authorities discovered: {string.Join(", ", result.Authorities)}");
            WriteVerbose($"AXFR targets tried: {string.Join(", ", result.TriedServers)}");

            if (IncludeTransferSummary.IsPresent) {
                WriteObject(result);
            }

            WriteObject(result.RecordSets, true);
            return;
        }

        await foreach (var rrset in ResolverZoneTransferWorkflow.StreamAsync(new ResolverExecutionTarget {
            DisplayName = $"tcp@{Server}:{Port}",
            ExplicitEndpoint = new DnsResolverEndpoint {
                Transport = Transport.Tcp,
                Host = Server,
                Port = Port
            }
        }, Zone, cancellationToken: CancelToken).ConfigureAwait(false)) {
            WriteObject(rrset);
        }
    }
}
