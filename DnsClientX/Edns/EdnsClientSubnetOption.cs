namespace DnsClientX;

/// <summary>
/// Represents an EDNS Client Subnet option to include in queries.
/// </summary>
public readonly record struct EdnsClientSubnetOption(string Subnet);
