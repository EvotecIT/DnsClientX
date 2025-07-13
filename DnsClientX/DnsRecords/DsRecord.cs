namespace DnsClientX;

/// <summary>
/// Represents a DS record used for DNSSEC validation.
/// </summary>
public sealed class DsRecord {
    public ushort KeyTag { get; }
    public DnsKeyAlgorithm Algorithm { get; }
    public byte DigestType { get; }
    public string Digest { get; }

    public DsRecord(ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest) {
        KeyTag = keyTag;
        Algorithm = algorithm;
        DigestType = digestType;
        Digest = digest;
    }
}
