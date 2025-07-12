namespace DnsClientX;

/// <summary>
/// Represents a DNAME record providing non-terminal redirection.
/// </summary>
public sealed class DnameRecord {
    public string Target { get; }

    public DnameRecord(string target) {
        Target = target;
    }
}
