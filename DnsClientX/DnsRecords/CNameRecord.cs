namespace DnsClientX;
/// <summary>
/// Represents a CNAME record containing an alias.
/// </summary>
/// <remarks>
/// Defined in <a href="https://www.rfc-editor.org/rfc/rfc1035">RFC 1035</a> section 3.3.1.
/// </remarks>
public sealed class CNameRecord {
    /// <summary>Gets the canonical name.</summary>
    public string CName { get; }
    /// <summary>Initializes a new instance of the <see cref="CNameRecord"/> class.</summary>
    /// <param name="cname">Canonical name.</param>
    public CNameRecord(string cname) {
        CName = cname;
    }
}
