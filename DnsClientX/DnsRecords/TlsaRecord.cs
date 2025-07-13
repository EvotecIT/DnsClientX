namespace DnsClientX;

/// <summary>
/// Represents a TLSA record for DANE validation.
/// </summary>
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
