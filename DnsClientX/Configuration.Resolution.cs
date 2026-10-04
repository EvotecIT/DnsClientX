using System;

namespace DnsClientX;

public partial class Configuration {
    /// <summary>
    /// Gets or sets the explicit UDP/TCP resolver used to obtain an endpoint hostname's address.
    /// Its Host must be an IP literal. Null uses system hostname resolution. Explicit bootstrap
    /// failures do not fall back to system DNS. HTTP bootstrap requires .NET 8 or later and
    /// does not support HTTP/3, gRPC, or an HTTP proxy.
    /// </summary>
    public DnsResolverEndpoint? BootstrapResolver { get; set; }

    internal DnsServerResolutionInfo? ServerResolution { get; set; }

    /// <summary>Reloads servers, search suffixes, and supported policy for a system endpoint.</summary>
    /// <param name="fallback">Optional fallback when the operating system exposes no resolver.</param>
    public void RefreshSystemDns(SystemDnsFallback fallback = SystemDnsFallback.None) {
        if (BuiltInEndpoint != DnsEndpoint.System && BuiltInEndpoint != DnsEndpoint.SystemTcp) {
            throw new InvalidOperationException("Only system DNS endpoints can refresh operating-system configuration.");
        }
        SystemDnsConfiguration refreshed = SystemInformation.GetDnsConfiguration(refresh: true, fallback: fallback);
        if (!refreshed.HasDnsServers) {
            throw new InvalidOperationException(
                "No DNS servers were exposed by the operating system. Configure an explicit resolver or opt in to SystemDnsFallback.PublicResolvers.");
        }
        lock (selectionLock) {
            SystemDnsConfiguration = refreshed;
            hostnames.Clear();
            hostnames.AddRange(refreshed.DnsServers);
            hostnameIndex = 0;
            policyHostnameIndex = 0;
            lock (unavailable) unavailable.Clear();
            SelectHostNameStrategyCore();
        }
    }
}
