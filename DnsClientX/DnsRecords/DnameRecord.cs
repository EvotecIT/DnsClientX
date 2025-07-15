namespace DnsClientX;

/// <summary>
/// Represents a DNAME record providing non-terminal redirection.
/// </summary>
/// <remarks>
/// See <a href="https://www.rfc-editor.org/rfc/rfc6672">RFC 6672</a> for details.
/// </remarks>
public sealed class DnameRecord {
    /// <summary>Gets the domain name that the original name is aliased to.</summary>
    public string Target { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DnameRecord"/> class.
    /// </summary>
    /// <param name="target">The canonical domain name.</param>
    public DnameRecord(string target) {
        Target = target;
    }
}
