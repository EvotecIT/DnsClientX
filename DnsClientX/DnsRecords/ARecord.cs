namespace DnsClientX;
using System.Net;
/// <summary>
/// Represents an A record containing an IPv4 address.
/// </summary>
/// <remarks>
/// Defined in <a href="https://www.rfc-editor.org/rfc/rfc1035">RFC 1035</a> section 3.4.1.
/// </remarks>
public sealed class ARecord {
    /// <summary>Gets the IP address.</summary>
    public IPAddress Address { get; }
    /// <summary>Initializes a new instance of the <see cref="ARecord"/> class.</summary>
    /// <param name="address">IPv4 address.</param>
    public ARecord(IPAddress address) {
        Address = address;
    }
}
