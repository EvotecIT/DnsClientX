using System;
using System.Management.Automation;

namespace DnsClientX.PowerShell;

/// <summary>
/// <para type="synopsis">Creates a caller-owned optional DNS privacy transport from a trusted local provider.</para>
/// <para type="description">Loads the selected provider DLL and its dependencies, then creates a transport from a DNSCrypt resolver stamp or an ODoH target and relay. Loading a DLL executes trusted local code. Dispose clients before disposing the transport.</para>
/// <para type="description">Pass the transport's CreateConfiguration() result to Resolve-Dns -Configuration. DNSCrypt accepts an optional anonymizing relay stamp; ODoH requires an explicit relay endpoint or stamp. This command does not publish or install packages.</para>
/// </summary>
/// <example>
/// <para>Query through an explicitly selected ODoH relay and target</para>
/// <code>$transport = New-DnsPrivacyTransport -ProviderPath ./DnsClientX.Odoh.dll -Resolver https://target.example/dns-query -Relay https://relay.example/dns-query
/// try { Resolve-Dns example.com -Configuration $transport.CreateConfiguration() -FullResponse } finally { $transport.Dispose() }</code>
/// </example>
[Cmdlet(VerbsCommon.New, "DnsPrivacyTransport")]
[OutputType(typeof(IDnsWireTransportProvider))]
public sealed class CmdletNewDnsPrivacyTransport : PSCmdlet {
    /// <summary><para type="description">Trusted local provider DLL path, with dependencies beside it. A provider DLL is required to create a transport; omitting this parameter reports MissingDnsPrivacyProvider with setup guidance.</para></summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string ProviderPath { get; set; } = string.Empty;
    /// <summary><para type="description">Trusted DNSCrypt resolver stamp or ODoH target stamp/HTTPS endpoint.</para></summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Resolver { get; set; } = string.Empty;
    /// <summary><para type="description">Optional DNSCrypt relay stamp; required ODoH relay stamp/HTTPS endpoint.</para></summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Relay { get; set; }
    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (string.IsNullOrWhiteSpace(ProviderPath)) {
            ThrowTerminatingError(new ErrorRecord(
                new ArgumentException("Supply -ProviderPath with a trusted local privacy provider DLL and its dependencies. The core DnsClientX package does not include ODoH or DNSCrypt providers."),
                "MissingDnsPrivacyProvider", ErrorCategory.InvalidArgument, null));
            return;
        }
        string path = SessionState.Path.GetUnresolvedProviderPathFromPSPath(ProviderPath, out ProviderInfo provider, out _);
        if (!string.Equals(provider.Name, "FileSystem", StringComparison.OrdinalIgnoreCase)) {
            throw new ArgumentException("The privacy provider must be a local filesystem DLL.");
        }
        IDnsWireTransportProvider transport = DnsWireTransportProviderLoader.Load(path, Resolver, Relay);
        try { WriteObject(transport); }
        catch { transport.Dispose(); throw; }
    }
}
