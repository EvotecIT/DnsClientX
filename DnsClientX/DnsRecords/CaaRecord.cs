namespace DnsClientX;
/// <summary>
/// Represents a CAA record specifying certificate authority policy.
/// </summary>
/// <remarks>
/// The CAA record type is documented in <a href="https://www.rfc-editor.org/rfc/rfc8659">RFC 8659</a>.
/// </remarks>
public sealed class CaaRecord {
    public byte Flags { get; }
    public string Tag { get; }
    public string Value { get; }

    public CaaRecord(byte flags, string tag, string value) {
        Flags = flags;
        Tag = tag;
        Value = value;
    }
}

