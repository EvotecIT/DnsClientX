namespace DnsClientX;
using System.Net;
/// <summary>
/// Represents an A record containing an IPv4 address.
/// </summary>
public sealed class ARecord {
    /// <summary>Gets the IP address.</summary>
    public IPAddress Address { get; }
    /// <summary>Initializes a new instance of the <see cref="ARecord"/> class.</summary>
    /// <param name="address">IPv4 address.</param>
    public ARecord(IPAddress address) {
        Address = address;
    }
}
