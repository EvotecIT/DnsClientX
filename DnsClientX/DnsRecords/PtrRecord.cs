namespace DnsClientX;
/// <summary>
/// Represents a PTR record mapping an IP address to a hostname.
/// </summary>
/// <remarks>
/// PTR records are commonly used for reverse DNS lookups.
/// </remarks>
public sealed class PtrRecord {
    /// <summary>Gets the pointer host name.</summary>
    public string Pointer { get; }

    /// <summary>Initializes a new instance of the <see cref="PtrRecord"/> class.</summary>
    /// <param name="pointer">Pointer host name.</param>
    public PtrRecord(string pointer) => Pointer = pointer;
}

