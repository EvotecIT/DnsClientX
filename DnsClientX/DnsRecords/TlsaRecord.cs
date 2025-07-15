namespace DnsClientX;

/// <summary>
/// Represents a TLSA record for DANE validation.
/// </summary>
/// <remarks>
/// See <a href="https://www.rfc-editor.org/rfc/rfc6698">RFC 6698</a> for the specification.
/// </remarks>
public sealed class TlsaRecord {
    /// <summary>Gets the certificate usage field.</summary>
    public byte CertificateUsage { get; }
    /// <summary>Gets the selector field specifying which part of the certificate is matched.</summary>
    public byte Selector { get; }
    /// <summary>Gets the matching type describing how the association data is compared.</summary>
    public byte MatchingType { get; }
    /// <summary>Gets the certificate association data.</summary>
    public string AssociationData { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TlsaRecord"/> class.
    /// </summary>
    /// <param name="certificateUsage">The certificate usage.</param>
    /// <param name="selector">The certificate selector.</param>
    /// <param name="matchingType">The matching type.</param>
    /// <param name="associationData">The association data.</param>
    public TlsaRecord(byte certificateUsage, byte selector, byte matchingType, string associationData) {
        CertificateUsage = certificateUsage;
        Selector = selector;
        MatchingType = matchingType;
        AssociationData = associationData;
    }
}
