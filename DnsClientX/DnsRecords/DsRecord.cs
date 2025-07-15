namespace DnsClientX;

/// <summary>
/// Represents a DS record used for DNSSEC validation.
/// </summary>
public sealed class DsRecord {
    /// <summary>Gets the key tag identifying the DNSKEY record.</summary>
    public ushort KeyTag { get; }
    /// <summary>Gets the algorithm used by the referenced DNSKEY.</summary>
    public DnsKeyAlgorithm Algorithm { get; }
    /// <summary>Gets the digest type.</summary>
    public byte DigestType { get; }
    /// <summary>Gets the digest value in hexadecimal form.</summary>
    public string Digest { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DsRecord"/> class.
    /// </summary>
    /// <param name="keyTag">The key tag.</param>
    /// <param name="algorithm">The DNSSEC algorithm.</param>
    /// <param name="digestType">The digest type.</param>
    /// <param name="digest">The digest string.</param>
    public DsRecord(ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest) {
        KeyTag = keyTag;
        Algorithm = algorithm;
        DigestType = digestType;
        Digest = digest;
    }
}
