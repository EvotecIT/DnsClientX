namespace DnsClientX;
/// <summary>
/// Represents a TXT record containing one or more text strings.
/// </summary>
public sealed class TxtRecord {
    /// <summary>Gets the text strings.</summary>
    public string[] Text { get; }

    /// <summary>Initializes a new instance of the <see cref="TxtRecord"/> class.</summary>
    /// <param name="text">Text strings.</param>
    public TxtRecord(string[] text) => Text = text;
}

