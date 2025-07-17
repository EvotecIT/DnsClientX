namespace DnsClientX;
/// <summary>
/// Represents a TXT record containing text data.
/// </summary>
/// <remarks>
/// TXT records are often used for miscellaneous domain metadata.
/// </remarks>
public sealed class TxtRecord {
    /// <summary>Gets the TXT record text value.</summary>
    public string Text { get; }

    /// <summary>Initializes a new instance of the <see cref="TxtRecord"/> class.</summary>
    /// <param name="text">Text strings that will be concatenated.</param>
    public TxtRecord(string[] text) => Text = string.Concat(text);

    /// <summary>Initializes a new instance of the <see cref="TxtRecord"/> class.</summary>
    /// <param name="text">Text string.</param>
    public TxtRecord(string text) => Text = text;
}

