namespace DnsClientX;

/// <summary>
/// Represents a DNSKEY record containing public key information.
/// </summary>
/// <remarks>
/// DNSSEC uses this record type for key distribution as specified in <a href="https://www.rfc-editor.org/rfc/rfc4034">RFC 4034</a>.
/// </remarks>
public sealed class DnsKeyRecord {
    /// <summary>Gets the DNSKEY flags.</summary>
    public ushort Flags { get; }
    /// <summary>Gets the protocol number. This should always be 3.</summary>
    public byte Protocol { get; }
    /// <summary>Gets the algorithm identifier.</summary>
    public DnsKeyAlgorithm Algorithm { get; }
    /// <summary>Gets the base64-encoded public key.</summary>
    public string PublicKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DnsKeyRecord"/> class.
    /// </summary>
    /// <param name="flags">Key flags.</param>
    /// <param name="protocol">The protocol value, typically 3.</param>
    /// <param name="algorithm">The signing algorithm.</param>
    /// <param name="publicKey">The public key data.</param>
    public DnsKeyRecord(ushort flags, byte protocol, DnsKeyAlgorithm algorithm, string publicKey) {
        Flags = flags;
        Protocol = protocol;
        Algorithm = algorithm;
        PublicKey = publicKey;
    }
}
