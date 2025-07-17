namespace DnsClientX;

/// <summary>
/// Represents a tag/value pair used in TXT records.
/// </summary>
public sealed class TagRecord {
    /// <summary>Gets the tag name.</summary>
    public string Tag { get; }

    /// <summary>Gets the tag value.</summary>
    public string Value { get; }

    /// <summary>Initializes a new instance of the <see cref="TagRecord"/> class.</summary>
    /// <param name="tag">The tag name.</param>
    /// <param name="value">The tag value.</param>
    public TagRecord(string tag, string value) {
        Tag = tag;
        Value = value;
    }
}
