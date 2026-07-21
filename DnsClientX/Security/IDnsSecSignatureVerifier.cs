namespace DnsClientX;

/// <summary>
/// Extends local DNSSEC validation with signature algorithms intentionally omitted from the
/// dependency-free DnsClientX core.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe. Input arrays are read-only for the duration of a call.
/// Returning <see langword="false"/> rejects the signature; unsupported algorithms must return
/// <see langword="false"/> from <see cref="SupportsAlgorithm"/> so validation reports an
/// indeterminate algorithm rather than a bogus signature.
/// </remarks>
public interface IDnsSecSignatureVerifier {
    /// <summary>Gets a short diagnostic name for this verifier.</summary>
    string Name { get; }

    /// <summary>Returns whether this verifier implements the specified DNSSEC algorithm.</summary>
    bool SupportsAlgorithm(DnsKeyAlgorithm algorithm);

    /// <summary>Verifies an RFC 4034 canonical RRset signature.</summary>
    bool Verify(DnsKeyAlgorithm algorithm, byte[] publicKey, byte[] data, byte[] signature);
}
