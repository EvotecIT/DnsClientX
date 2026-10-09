using System;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;

namespace DnsClientX.DnsSec.EdDsa;

/// <summary>Verifies RFC 6605 ECDSA and RFC 8080 EdDSA DNSSEC signatures across all targets with Bouncy Castle.</summary>
public sealed class EdDsaDnsSecSignatureVerifier : IDnsSecSignatureVerifier {
    /// <inheritdoc />
    public string Name => "BouncyCastle.Cryptography ECDSA/EdDSA";

    /// <inheritdoc />
    public bool SupportsAlgorithm(DnsKeyAlgorithm algorithm) =>
        algorithm == DnsKeyAlgorithm.ECDSAP256SHA256 || algorithm == DnsKeyAlgorithm.ECDSAP384SHA384
        || algorithm == DnsKeyAlgorithm.ED25519 || algorithm == DnsKeyAlgorithm.ED448;

    /// <inheritdoc />
    public bool Verify(DnsKeyAlgorithm algorithm, byte[] publicKey, byte[] data, byte[] signature) {
        if (publicKey == null) throw new ArgumentNullException(nameof(publicKey));
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (signature == null) throw new ArgumentNullException(nameof(signature));
        try {
            switch (algorithm) {
                case DnsKeyAlgorithm.ECDSAP256SHA256:
                case DnsKeyAlgorithm.ECDSAP384SHA384:
                    return VerifyEcdsa(algorithm, publicKey, data, signature);
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

    private static bool VerifyEcdsa(DnsKeyAlgorithm algorithm, byte[] publicKey, byte[] data, byte[] signature) {
        int width = algorithm == DnsKeyAlgorithm.ECDSAP256SHA256 ? 32 : 48;
        if (publicKey.Length != width * 2 || signature.Length != width * 2) return false;
        var curve = SecNamedCurves.GetByName(width == 32 ? "secp256r1" : "secp384r1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
        var encodedPoint = new byte[publicKey.Length + 1];
        encodedPoint[0] = 4;
        Buffer.BlockCopy(publicKey, 0, encodedPoint, 1, publicKey.Length);
        var key = new ECPublicKeyParameters(curve.Curve.DecodePoint(encodedPoint), domain);
        IDigest digest = width == 32 ? new Sha256Digest() : new Sha384Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        var verifier = new ECDsaSigner();
        verifier.Init(false, key);
        return verifier.VerifySignature(hash, new BigInteger(1, signature, 0, width),
            new BigInteger(1, signature, width, width));
    }
}
