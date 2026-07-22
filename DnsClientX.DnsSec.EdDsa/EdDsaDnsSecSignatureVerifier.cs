using System;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace DnsClientX.DnsSec.EdDsa;

/// <summary>Verifies RFC 8080 Ed25519 and Ed448 DNSSEC signatures with Bouncy Castle.</summary>
public sealed class EdDsaDnsSecSignatureVerifier : IDnsSecSignatureVerifier {
    /// <inheritdoc />
    public string Name => "BouncyCastle.Cryptography EdDSA";

    /// <inheritdoc />
    public bool SupportsAlgorithm(DnsKeyAlgorithm algorithm) =>
        algorithm == DnsKeyAlgorithm.ED25519 || algorithm == DnsKeyAlgorithm.ED448;

    /// <inheritdoc />
    public bool Verify(DnsKeyAlgorithm algorithm, byte[] publicKey, byte[] data, byte[] signature) {
        if (publicKey == null) throw new ArgumentNullException(nameof(publicKey));
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (signature == null) throw new ArgumentNullException(nameof(signature));
        try {
            switch (algorithm) {
                case DnsKeyAlgorithm.ED25519:
                    if (publicKey.Length != Ed25519PublicKeyParameters.KeySize || signature.Length != 64) {
                        return false;
                    }
                    var ed25519 = new Ed25519Signer();
                    ed25519.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
                    ed25519.BlockUpdate(data, 0, data.Length);
                    return ed25519.VerifySignature(signature);
                case DnsKeyAlgorithm.ED448:
                    if (publicKey.Length != Ed448PublicKeyParameters.KeySize || signature.Length != 114) {
                        return false;
                    }
                    var ed448 = new Ed448Signer(Array.Empty<byte>());
                    ed448.Init(false, new Ed448PublicKeyParameters(publicKey, 0));
                    ed448.BlockUpdate(data, 0, data.Length);
                    return ed448.VerifySignature(signature);
                default:
                    return false;
            }
        } catch (ArgumentException) {
            return false;
        }
    }
}
