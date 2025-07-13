namespace DnsClientX;
using System.Net;
/// <summary>
/// Represents an AAAA record containing an IPv6 address.
/// </summary>
public sealed class AAAARecord {
    /// <summary>Gets the IP address.</summary>
    public IPAddress Address { get; }
    /// <summary>Initializes a new instance of the <see cref="AAAARecord"/> class.</summary>
    /// <param name="address">IPv6 address.</param>
    public AAAARecord(IPAddress address) {
        Address = address;
    }
}
