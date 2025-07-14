namespace DnsClientX;

/// <summary>
/// Represents a TLSA record for DANE validation.
/// </summary>
/// <remarks>
/// See <a href="https://www.rfc-editor.org/rfc/rfc6698">RFC 6698</a> for the specification.
/// </remarks>
public sealed class TlsaRecord {
    public byte CertificateUsage { get; }
    public byte Selector { get; }
    public byte MatchingType { get; }
    public string AssociationData { get; }

    public TlsaRecord(byte certificateUsage, byte selector, byte matchingType, string associationData) {
        CertificateUsage = certificateUsage;
        Selector = selector;
        MatchingType = matchingType;
        AssociationData = associationData;
    }
}
