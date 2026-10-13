using System;
using System.Management.Automation;
using System.Net;
using System.Threading.Tasks;

namespace DnsClientX.PowerShell;

/// <summary>
/// <para type="synopsis">Discovers encrypted resolvers designated by an explicit IP DNS resolver.</para>
/// <para type="description">Returns bounded RFC 9462 candidates with explicit certificate verification outcomes.
/// Discovery does not switch resolvers. Only a verified, unexpired candidate can create an encrypted configuration.</para>
/// </summary>
/// <example>
/// <para>Discover and inspect designated resolvers.</para>
/// <code>Get-DnsDesignatedResolver -Server 192.0.2.53 | Select-Object Binding,Verification,VerificationError,ExpiresAt</code>
/// </example>
/// <example>
/// <para>Use a verified designation for a query.</para>
/// <code>$candidate = Get-DnsDesignatedResolver -Server 192.0.2.53 | Where-Object Verification -eq Verified | Select-Object -First 1
/// if ($null -eq $candidate) { throw 'No verified designated resolver was found.' }
/// Resolve-Dns -Name example.com -Configuration ($candidate.CreateConfiguration())</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "DnsDesignatedResolver", DefaultParameterSetName = "Server")]
[OutputType(typeof(DnsDesignatedResolver))]
public sealed class CmdletGetDnsDesignatedResolver : AsyncPSCmdlet {
    /// <summary><para type="description">Original UDP or TCP resolver's IP address.</para></summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Server")]
    [ValidateNotNull]
    public IPAddress Server { get; set; } = IPAddress.Loopback;

    /// <summary><para type="description">Original resolver's DNS port.</para></summary>
    [Parameter(ParameterSetName = "Server")]
    [ValidateRange(1, 65535)]
    public int Port { get; set; } = 53;

    /// <summary><para type="description">Use TCP for discovery DNS queries instead of UDP.</para></summary>
    [Parameter(ParameterSetName = "Server")]
    public SwitchParameter Tcp { get; set; }

    /// <summary><para type="description">Timeout for discovery DNS queries, in milliseconds.</para></summary>
    [Parameter(ParameterSetName = "Server")]
    [ValidateRange(1, int.MaxValue)]
    public int TimeOut { get; set; } = DnsClientX.Configuration.DefaultTimeout;

    /// <summary><para type="description">Explicit IP UDP/TCP resolver configuration to use for discovery.</para></summary>
    [Parameter(Mandatory = true, ParameterSetName = "Configuration", ValueFromPipeline = true)]
    [ValidateNotNull]
    public Configuration? Configuration { get; set; }

    /// <summary><para type="description">Core discovery bounds, ALPN preferences, and certificate trust policy.</para></summary>
    [Parameter]
    public DnsDesignatedResolverDiscoveryOptions? Options { get; set; }

    /// <inheritdoc/>
    protected override async Task ProcessRecordAsync() {
        Configuration configuration = Configuration ?? new Configuration(Server.ToString(), Tcp.IsPresent ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP) {
            Port = Port,
            TimeOut = TimeOut
        };
        using var client = ResolverExecutionClientFactory.CreateConfiguredClient(configuration);
        DnsDesignatedResolver[] candidates = await client.DiscoverDesignatedResolversAsync(Options, CancelToken).ConfigureAwait(false);
        WriteObject(candidates, true);
    }
}
