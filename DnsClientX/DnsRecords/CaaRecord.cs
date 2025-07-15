namespace DnsClientX;
/// <summary>
/// Represents a CAA record specifying certificate authority policy.
/// </summary>
/// <remarks>
/// The CAA record type is documented in <a href="https://www.rfc-editor.org/rfc/rfc8659">RFC 8659</a>.
/// </remarks>
public sealed class CaaRecord {
    /// <summary>Gets the flag value controlling critical handling.</summary>
    public byte Flags { get; }
    /// <summary>Gets the property tag defined by the certificate authority.</summary>
    public string Tag { get; }
    /// <summary>Gets the property value associated with the <see cref="Tag"/>.</summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CaaRecord"/> class.
    /// </summary>
    /// <param name="flags">The flags for the record.</param>
    /// <param name="tag">The property tag.</param>
    /// <param name="value">The property value.</param>
    public CaaRecord(byte flags, string tag, string value) {
        Flags = flags;
        Tag = tag;
        Value = value;
    }
}

