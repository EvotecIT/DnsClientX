namespace DnsClientX;

/// <summary>
/// Represents a DNSKEY record containing public key information.
/// </summary>
public sealed class DnsKeyRecord {
    public ushort Flags { get; }
    public byte Protocol { get; }
    public DnsKeyAlgorithm Algorithm { get; }
    public string PublicKey { get; }

    public DnsKeyRecord(ushort flags, byte protocol, DnsKeyAlgorithm algorithm, string publicKey) {
        Flags = flags;
        Protocol = protocol;
        Algorithm = algorithm;
        PublicKey = publicKey;
    }
}
