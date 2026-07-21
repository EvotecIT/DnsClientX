using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Contract tests for dependency-free DNSSEC cryptography and serial-time handling.
    /// </summary>
    public class DnsSecCryptoTests {
        /// <summary>Public key-tag projection uses the same RFC 4034 calculation as validation.</summary>
        [Fact]
        public void PublicKeyTagHelperUsesRfc4034Calculation() {
            Assert.Equal((ushort)20326, DnsSecValidator.ComputeKeyTag(
                "257 3 8 AwEAAaz/tAm8yTn4Mfeh5eyI96WSVexTBAvkMgJzkKTOiW1vkIbzxeF3+/4RgWOq7HrxRixHlFlExOLAJr5emLvN7SWXgnLh4+B5xQlNVz8Og8kvArMtNROxVQuCaSnIDdD5LKyWbRd2n9WGe2R8PzgCmr3EgVLrjyBxWezF0jLHwVN8efS3rCj/EWgvIWgb9tarpVUDK/b58Da+sqqls3eNbuv7pr+eoZG+SrDK6nWeL3c6H5Apxz7LjVc1uTIdsIXxuOLYA4/ilBmSVIzuDWfdRUfhHdY6+cn8HFRm+2hM8AnXGXws9555KrUB5qihylGa8subX2Nn6UwNR1AkUTV74bU="));
        }
        /// <summary>Verifies DNSSEC RSA/SHA-256 public-key decoding and signature checks.</summary>
        [Fact]
        public void RsaSha256_VerifiesDnsKeySignatureAndRejectsTampering() {
            using RSA rsa = RSA.Create(2048);
            RSAParameters parameters = rsa.ExportParameters(false);
            byte[] publicKey = EncodeRsaDnsKey(parameters);
            var key = new DnsSecKey("example.com", 257, 3, 8, publicKey);
            byte[] data = { 1, 3, 3, 7, 9 };
            byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            Assert.True(DnsSecCrypto.Verify(key, data, signature));
            data[0] ^= 1;
            Assert.False(DnsSecCrypto.Verify(key, data, signature));
        }

#if NET5_0_OR_GREATER
        /// <summary>Verifies DNSSEC ECDSA P-256 signatures use the RFC fixed-width encoding.</summary>
        [Fact]
        public void EcdsaP256_VerifiesFixedWidthDnsSecSignature() {
            using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            ECParameters parameters = ecdsa.ExportParameters(false);
            var publicKey = new byte[64];
            Buffer.BlockCopy(parameters.Q.X!, 0, publicKey, 0, 32);
            Buffer.BlockCopy(parameters.Q.Y!, 0, publicKey, 32, 32);
            var key = new DnsSecKey("example.com", 257, 3, 13, publicKey);
            byte[] data = { 2, 4, 6, 8 };
            byte[] signature = ecdsa.SignData(data, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

            Assert.True(DnsSecCrypto.Verify(key, data, signature));
            signature[0] ^= 1;
            Assert.False(DnsSecCrypto.Verify(key, data, signature));
        }
#endif

        /// <summary>Verifies RFC serial arithmetic across the 32-bit signature-time rollover.</summary>
        [Fact]
        public void SignatureTimeValidation_HandlesSerialRollover() {
            var signature = new DnsSecSignature("example.com", 1, DnsRecordType.A, 8, 2,
                300, 10, uint.MaxValue - 10, 1234, "example.com", new byte[] { 1 });
            DateTimeOffset afterRollover = DateTimeOffset.FromUnixTimeSeconds((long)uint.MaxValue + 5);

            Assert.True(DnsSecWire.SignatureTimeIsValid(signature, afterRollover));
            Assert.False(DnsSecWire.SignatureTimeIsValid(signature,
                DateTimeOffset.FromUnixTimeSeconds((long)uint.MaxValue + 20)));
        }

        private static byte[] EncodeRsaDnsKey(RSAParameters parameters) {
            byte[] exponent = parameters.Exponent!;
            byte[] modulus = parameters.Modulus!;
            var value = new List<byte>(exponent.Length + modulus.Length + 3);
            if (exponent.Length < 256) {
                value.Add((byte)exponent.Length);
            } else {
                value.Add(0);
                value.Add((byte)(exponent.Length >> 8));
                value.Add((byte)exponent.Length);
            }
            value.AddRange(exponent);
            value.AddRange(modulus);
            return value.ToArray();
        }
    }
}
