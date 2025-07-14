namespace DnsClientX;

/// <summary>
/// Represents a DNAME record providing non-terminal redirection.
/// </summary>
/// <remarks>
/// See <a href="https://www.rfc-editor.org/rfc/rfc6672">RFC 6672</a> for details.
/// </remarks>
public sealed class DnameRecord {
    public string Target { get; }

    public DnameRecord(string target) {
        Target = target;
    }
}
