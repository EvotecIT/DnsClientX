using System;
using System.IO;
using System.Security.Cryptography;

namespace DnsClientX {
    internal static class DnsSecCrypto {
        internal static ushort ComputeKeyTag(ushort flags, byte protocol, byte algorithm, byte[] publicKey) {
            using var stream = new MemoryStream();
            stream.WriteByte((byte)(flags >> 8));
            stream.WriteByte((byte)flags);
            stream.WriteByte(protocol);
            stream.WriteByte(algorithm);
            stream.Write(publicKey, 0, publicKey.Length);
            byte[] rdata = stream.ToArray();
            uint accumulator = 0;
            for (int i = 0; i < rdata.Length; i++) accumulator += (i & 1) == 0 ? (uint)rdata[i] << 8 : rdata[i];
            accumulator += accumulator >> 16;
            return (ushort)accumulator;
        }

        internal static bool TryComputeDsDigest(string owner, DnsSecKey key, byte digestType, out byte[] digest) {
            digest = Array.Empty<byte>();
            using var stream = new MemoryStream();
            byte[] name = DnsWireNameCodec.ToCanonicalWire(owner);
            stream.Write(name, 0, name.Length);
            stream.WriteByte((byte)(key.Flags >> 8));
            stream.WriteByte((byte)key.Flags);
            stream.WriteByte(key.Protocol);
            stream.WriteByte(key.Algorithm);
            stream.Write(key.PublicKey, 0, key.PublicKey.Length);
            byte[] value = stream.ToArray();
            using HashAlgorithm? hash = digestType switch {
                1 => SHA1.Create(),
                2 => SHA256.Create(),
                4 => SHA384.Create(),
                _ => null
            };
            if (hash == null) return false;
            digest = hash.ComputeHash(value);
            return true;
        }

        internal static bool IsSupportedAlgorithm(byte algorithm, IDnsSecSignatureVerifier? extension = null) {
            if (algorithm == 5 || algorithm == 7 || algorithm == 8 || algorithm == 10) return true;
#if NET5_0_OR_GREATER
            if (algorithm == 13 || algorithm == 14) return true;
#endif
            return extension?.SupportsAlgorithm((DnsKeyAlgorithm)algorithm) == true;
        }

        internal static bool Verify(DnsSecKey key, byte[] data, byte[] signature,
            IDnsSecSignatureVerifier? extension = null) {
            try {
                switch (key.Algorithm) {
                    case 5:
                    case 7:
                    case 8:
                    case 10:
                        if (!TryGetRsaParameters(key.PublicKey, out RSAParameters rsaParameters)) return false;
                        using (RSA rsa = RSA.Create()) {
                            rsa.ImportParameters(rsaParameters);
                            HashAlgorithmName hash = key.Algorithm == 8 ? HashAlgorithmName.SHA256 :
                                key.Algorithm == 10 ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA1;
                            return rsa.VerifyData(data, signature, hash, RSASignaturePadding.Pkcs1);
                        }
#if NET5_0_OR_GREATER
                    case 13:
                    case 14:
                        int coordinateLength = key.Algorithm == 13 ? 32 : 48;
                        if (key.PublicKey.Length != coordinateLength * 2 || signature.Length != coordinateLength * 2) return false;
                        var parameters = new ECParameters {
                            Curve = key.Algorithm == 13 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384,
                            Q = new ECPoint {
                                X = Slice(key.PublicKey, 0, coordinateLength),
                                Y = Slice(key.PublicKey, coordinateLength, coordinateLength)
                            }
                        };
                        using (ECDsa ecdsa = ECDsa.Create(parameters)) {
                            HashAlgorithmName hash = key.Algorithm == 13 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA384;
                            return ecdsa.VerifyData(data, signature, hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                        }
#endif
                    default:
                        return extension?.SupportsAlgorithm((DnsKeyAlgorithm)key.Algorithm) == true
                            && extension.Verify((DnsKeyAlgorithm)key.Algorithm, key.PublicKey, data, signature);
                }
            } catch (Exception exception) when (exception is CryptographicException || exception is ArgumentException) {
                return false;
            }
        }

        private static bool TryGetRsaParameters(byte[] keyData, out RSAParameters parameters) {
            parameters = default;
            if (keyData.Length < 3) return false;
            int position = 0;
            int exponentLength = keyData[position++];
            if (exponentLength == 0) {
                if (keyData.Length < 4) return false;
                exponentLength = (keyData[position++] << 8) | keyData[position++];
            }
            if (exponentLength == 0 || position + exponentLength >= keyData.Length) return false;
            parameters = new RSAParameters {
                Exponent = Slice(keyData, position, exponentLength),
                Modulus = Slice(keyData, position + exponentLength, keyData.Length - position - exponentLength)
            };
            return true;
        }

        private static byte[] Slice(byte[] value, int offset, int count) {
            var result = new byte[count];
            Buffer.BlockCopy(value, offset, result, 0, count);
            return result;
        }
    }
}
