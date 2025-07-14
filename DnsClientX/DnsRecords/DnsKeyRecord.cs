namespace DnsClientX;

/// <summary>
/// Represents a DNSKEY record containing public key information.
/// </summary>
/// <remarks>
/// DNSSEC uses this record type for key distribution as specified in <a href="https://www.rfc-editor.org/rfc/rfc4034">RFC 4034</a>.
/// </remarks>
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
