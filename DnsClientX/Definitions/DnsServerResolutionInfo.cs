namespace DnsClientX;

/// <summary>Describes how the connected resolver's hostname was resolved.</summary>
public sealed class DnsServerResolutionInfo {
    internal DnsServerResolutionInfo(string hostname, string? address, string? bootstrapResolver,
        bool servedFromCache, bool usedStaleAddress, string? error) {
        Hostname = hostname;
        Address = address;
        BootstrapResolver = bootstrapResolver;
        ServedFromCache = servedFromCache;
        UsedStaleAddress = usedStaleAddress;
        Error = error;
    }

    /// <summary>Gets the endpoint hostname retained for TLS SNI and certificate validation.</summary>
    public string Hostname { get; }
    /// <summary>Gets the selected network address, or null when resolution failed.</summary>
    public string? Address { get; }
    /// <summary>Gets the explicit bootstrap endpoint, or null for system hostname resolution.</summary>
    public string? BootstrapResolver { get; }
    /// <summary>Gets whether hostname resolution reused a cached entry.</summary>
    public bool ServedFromCache { get; }
    /// <summary>Gets whether a failed lookup reused a still-eligible stale address.</summary>
    public bool UsedStaleAddress { get; }
    /// <summary>Gets the hostname lookup error, including an error masked by eligible stale reuse.</summary>
    public string? Error { get; }
}
