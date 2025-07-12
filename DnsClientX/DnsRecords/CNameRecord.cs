namespace DnsClientX;
/// <summary>
/// Represents a CNAME record containing an alias.
/// </summary>
public sealed class CNameRecord {
    /// <summary>Gets the canonical name.</summary>
    public string CName { get; }
    /// <summary>Initializes a new instance of the <see cref="CNameRecord"/> class.</summary>
    /// <param name="cname">Canonical name.</param>
    public CNameRecord(string cname) {
        CName = cname;
    }
}
