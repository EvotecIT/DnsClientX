namespace DnsClientX;
/// <summary>
/// Represents a CAA record specifying certificate authority policy.
/// </summary>
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

