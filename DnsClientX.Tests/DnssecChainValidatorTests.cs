using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the DNSSEC chain validation helper.
    /// </summary>
    public class DnssecChainValidatorTests {
        private static byte[] DomainToWireFormat(string domain) {
            if (string.IsNullOrEmpty(domain) || domain == ".") return new byte[] { 0 };
            string[] labels = domain.TrimEnd('.').Split('.');
            var data = new List<byte>();
            foreach (string label in labels) {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(label.ToLowerInvariant());
                data.Add((byte)bytes.Length);
                data.AddRange(bytes);
            }
            data.Add(0);
            return data.ToArray();
        }

        private static ushort ComputeKeyTag(ushort flags, byte protocol, DnsKeyAlgorithm algorithm, byte[] publicKey) {
            byte[] rdata = new byte[4 + publicKey.Length];
            BinaryPrimitives.WriteUInt16BigEndian(rdata, flags);
            rdata[2] = protocol;
            rdata[3] = (byte)algorithm;
            Buffer.BlockCopy(publicKey, 0, rdata, 4, publicKey.Length);
            uint acc = 0;
            for (int i = 0; i < rdata.Length; i++) {
                acc += (i & 1) == 0 ? (uint)rdata[i] << 8 : rdata[i];
            }
            acc += acc >> 16;
            return (ushort)(acc & 0xFFFF);
        }

        private static string ComputeDigest(string name, ushort flags, byte protocol, DnsKeyAlgorithm algorithm, byte[] publicKey,
            byte digestType = 2) {
            byte[] owner = DomainToWireFormat(name);
            byte[] rdata = new byte[4 + publicKey.Length];
            BinaryPrimitives.WriteUInt16BigEndian(rdata, flags);
            rdata[2] = protocol;
            rdata[3] = (byte)algorithm;
            Buffer.BlockCopy(publicKey, 0, rdata, 4, publicKey.Length);
            byte[] message = new byte[owner.Length + rdata.Length];
            Buffer.BlockCopy(owner, 0, message, 0, owner.Length);
            Buffer.BlockCopy(rdata, 0, message, owner.Length, rdata.Length);
            using HashAlgorithm hash = digestType switch {
                1 => SHA1.Create(),
                2 => SHA256.Create(),
                4 => SHA384.Create(),
                _ => throw new ArgumentOutOfRangeException(nameof(digestType))
            };
            byte[] digestBytes = hash.ComputeHash(message);
            return BitConverter.ToString(digestBytes).Replace("-", string.Empty).ToUpperInvariant();
        }

        private static byte[] BuildSignedData(string name, int ttl, DateTime exp, DateTime inc, ushort keyTag, byte[] publicKey,
            ushort flags = 257, byte protocol = 3) {
            Type validator = typeof(DnsSecValidator);
            Type dnsKeyRec = validator.GetNestedType("DnsKeyRecord", System.Reflection.BindingFlags.NonPublic)!;
            Type rrsigRec = validator.GetNestedType("RrsigRecord", System.Reflection.BindingFlags.NonPublic)!;
            object dnsKey = Activator.CreateInstance(dnsKeyRec, name, flags, protocol, DnsKeyAlgorithm.RSASHA256, publicKey)!;
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(dnsKeyRec))!;
            list.Add(dnsKey);
            object rrsig = Activator.CreateInstance(rrsigRec, name, DnsRecordType.DNSKEY, DnsKeyAlgorithm.RSASHA256, (byte)name.TrimEnd('.').Split('.').Length, ttl, exp, inc, keyTag, name, Array.Empty<byte>())!;
            var method = validator.GetMethod("BuildDnskeySignedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            return (byte[])method.Invoke(null, new object[] { rrsig, list })!;
        }


        /// <summary>
        /// Validates a correct DNSSEC chain returns success.
        /// </summary>
        [Fact]
        public void ValidateChain_Succeeds() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            const DnsKeyAlgorithm alg = DnsKeyAlgorithm.RSASHA256;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} {(int)alg} {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, alg, pub);
            string digest = ComputeDigest(name, flags, protocol, alg, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag} {(int)alg} 2 {digest}" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY {(int)alg} 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.True(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Equal(string.Empty, msg);
        }

        /// <summary>
        /// Detects invalid signatures in the DNSSEC chain.
        /// </summary>
        [Fact]
        public void ValidateChain_InvalidSignature() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} 8 {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            string digest = ComputeDigest(name, flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag} 8 2 {digest}" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            sig[0] ^= 0xFF; // corrupt
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY 8 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("Invalid RRSIG", msg);
        }

        /// <summary>
        /// Fails validation when DNSKEY or RRSIG records are missing.
        /// </summary>
        [Fact]
        public void ValidateChain_MissingRecords() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "example.com.", Type = DnsRecordType.DS, TTL = 3600,
                        DataRaw = "1 8 2 0000000000000000000000000000000000000000000000000000000000000000" }
                }
            };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("Missing DNSKEY or RRSIG", msg);
        }

        /// <summary>An unrelated RRSIG cannot authenticate the supplied DNSKEY.</summary>
        [Fact]
        public void ValidateChain_IrrelevantSignature_ReturnsFalse() {
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = "example.com.", Type = DnsRecordType.DNSKEY, DataRaw = "257 3 8 AQ==" },
                    new DnsAnswer { Name = "example.com.", Type = DnsRecordType.RRSIG,
                        DataRaw = "A 8 2 3600 1900000000 1800000000 1 example.com. AQ==" }
                }
            };

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("DNSKEY", message);
        }

        /// <summary>
        /// Fails when DS records do not match any DNSKEY.
        /// </summary>
        [Fact]
        public void ValidateChain_NoMatchingDnsKey() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} 8 {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            string digest = ComputeDigest(name, flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600, DataRaw = $"{tag + 1} 8 2 {digest}" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY 8 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("No DNSKEY", msg);
        }

        /// <summary>
        /// Detects digest mismatches in DS records.
        /// </summary>
        [Fact]
        public void ValidateChain_DigestMismatch() {
            using RSA rsa = RSA.Create(1024);
            RSAParameters p = rsa.ExportParameters(true);
            byte[] pub = BuildPublicKey(p);
            string pubB64 = Convert.ToBase64String(pub);
            const string name = "example.com.";
            const ushort flags = 257;
            const byte protocol = 3;
            var dnskey = new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, TTL = 3600, DataRaw = $"{flags} {protocol} 8 {pubB64}" };
            ushort tag = ComputeKeyTag(flags, protocol, DnsKeyAlgorithm.RSASHA256, pub);
            var ds = new DnsAnswer { Name = name, Type = DnsRecordType.DS, TTL = 3600,
                DataRaw = $"{tag} 8 2 0000000000000000000000000000000000000000000000000000000000000000" };
            DateTime inception = DateTime.UtcNow.AddMinutes(-1);
            DateTime expiration = DateTime.UtcNow.AddHours(1);
            byte[] data = BuildSignedData(name, 3600, expiration, inception, tag, pub);
            byte[] sig = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string sigB64 = Convert.ToBase64String(sig);
            var rrsig = new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG, TTL = 3600, DataRaw = $"DNSKEY 8 2 3600 {(uint)(expiration - new DateTime(1970,1,1)).TotalSeconds} {(uint)(inception - new DateTime(1970,1,1)).TotalSeconds} {tag} {name} {sigB64}" };
            var response = new DnsResponse { Answers = new[] { dnskey, ds, rrsig } };
            Assert.False(DnsSecValidator.ValidateChain(response, out string msg));
            Assert.Contains("Digest mismatch", msg);
        }

        /// <summary>A signed DNSKEY RRset is locally consistent even without a supplied parent DS.</summary>
        [Fact]
        public void ValidateChain_SignedDnskeyWithoutDs_Succeeds() {
            DnsResponse response = CreateSignedKeyMaterial(includeDs: false);

            Assert.True(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Equal(string.Empty, message);
        }

        /// <summary>Supported parent DS digests authenticate the same signed DNSKEY material.</summary>
        [Theory]
        [InlineData((byte)1)]
        [InlineData((byte)4)]
        public void ValidateChain_SupportedDsDigestTypes_Succeed(byte digestType) {
            DnsResponse response = CreateSignedKeyMaterial(dsDigestType: digestType);

            Assert.True(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Equal(string.Empty, message);
        }

        /// <summary>A malformed supplied DS cannot be silently treated as absent.</summary>
        [Fact]
        public void ValidateChain_MalformedDs_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial(includeDs: false);
            var answers = response.Answers!.ToList();
            answers.Add(new DnsAnswer { Name = "example.com.", Type = DnsRecordType.DS, DataRaw = "not-a-ds" });
            response.Answers = answers.ToArray();

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("Failed to parse DS", message);
        }

        /// <summary>The RRSIG record owner must identify the signed DNSKEY RRset.</summary>
        [Fact]
        public void ValidateChain_RrsigWithOtherOwner_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial(rrsigOwner: "other.example.");

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("Invalid RRSIG", message);
        }

        /// <summary>A DS cannot authenticate another owner's DNSKEY with a matching tag and digest.</summary>
        [Fact]
        public void ValidateChain_DsWithOtherOwner_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial(dsOwner: "other.example.");

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("DS owner", message);
        }

        /// <summary>An unrelated DNSKEY owner is not part of the signed RRset.</summary>
        [Fact]
        public void ValidateChain_UnrelatedDnskeyOwner_DoesNotBreakValidRrset() {
            DnsResponse response = CreateSignedKeyMaterial(includeUnrelatedKey: true);

            Assert.True(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Equal(string.Empty, message);
        }

        /// <summary>A mathematically correct signature outside its validity period is unusable.</summary>
        [Theory]
        [InlineData(-4, -2)]
        [InlineData(2, 4)]
        public void ValidateChain_SignatureOutsideValidity_ReturnsFalse(int inceptionHours, int expirationHours) {
            DnsResponse response = CreateSignedKeyMaterial(
                inception: DateTime.UtcNow.AddHours(inceptionHours),
                expiration: DateTime.UtcNow.AddHours(expirationHours));

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("Invalid RRSIG", message);
        }

        /// <summary>A DNSKEY used to verify an RRSIG must be a protocol-3 zone key.</summary>
        [Theory]
        [InlineData((ushort)0, (byte)3)]
        [InlineData((ushort)257, (byte)2)]
        public void ValidateChain_IneligibleSigningKey_ReturnsFalse(ushort flags, byte protocol) {
            DnsResponse response = CreateSignedKeyMaterial(keyFlags: flags, keyProtocol: protocol);

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("Invalid RRSIG", message);
        }

        /// <summary>An extra unusable signature does not negate a valid signature for the RRset.</summary>
        [Fact]
        public void ValidateChain_ValidRrsetWithExtraInvalidSignature_Succeeds() {
            DnsResponse response = CreateSignedKeyMaterial();
            var answers = response.Answers!.ToList();
            answers.Add(new DnsAnswer { Name = "example.com.", Type = DnsRecordType.RRSIG,
                DataRaw = "DNSKEY 8 2 3600 1900000000 1800000000 1 example.com. AQ==" });
            response.Answers = answers.ToArray();

            Assert.True(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Equal(string.Empty, message);
        }

        /// <summary>A DS for an unsigned extra DNSKEY owner is not locally authenticated.</summary>
        [Fact]
        public void ValidateChain_DsForUnsignedOwner_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial(includeUnrelatedKey: true, includeUnsignedDs: true);

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("DS", message);
        }

        /// <summary>A missing owner gives a failure verdict instead of throwing from name canonicalization.</summary>
        [Fact]
        public void ValidateChain_EmptyDsOwner_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial(dsOwner: string.Empty);

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("owner", message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The entire hexadecimal DS digest field must be parsed, including later tokens.</summary>
        [Fact]
        public void ValidateChain_DsWithTrailingGarbage_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial();
            var answers = response.Answers!.ToArray();
            int dsIndex = Array.FindIndex(answers, answer => answer.Type == DnsRecordType.DS);
            answers[dsIndex].DataRaw += " garbage";
            response.Answers = answers;

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("DS", message);
        }

        /// <summary>An unparseable DNSKEY cannot be omitted from the signed RRset.</summary>
        [Fact]
        public void ValidateChain_MalformedDnskeyInSignedRrset_ReturnsFalse() {
            DnsResponse response = CreateSignedKeyMaterial();
            var answers = response.Answers!.ToList();
            answers.Add(new DnsAnswer { Name = "example.com.", Type = DnsRecordType.DNSKEY, DataRaw = "invalid" });
            response.Answers = answers.ToArray();

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("DNSKEY", message);
        }

        /// <summary>Malformed RSA parameters produce a false verdict instead of a crypto exception.</summary>
        [Fact]
        public void ValidateChain_MalformedRsaKey_ReturnsFalse() {
            const string name = "example.com.";
            string dnskeyData = "257 3 8 AQEB";
            ushort tag = DnsSecValidator.ComputeKeyTag(dnskeyData);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var response = new DnsResponse {
                Answers = new[] {
                    new DnsAnswer { Name = name, Type = DnsRecordType.DNSKEY, DataRaw = dnskeyData },
                    new DnsAnswer { Name = name, Type = DnsRecordType.RRSIG,
                        DataRaw = $"DNSKEY 8 2 3600 {now + 3600} {now - 3600} {tag} {name} AQ==" }
                }
            };

            Assert.False(DnsSecValidator.ValidateChain(response, out string message));
            Assert.Contains("Invalid RRSIG", message);
        }

        private static DnsResponse CreateSignedKeyMaterial(bool includeDs = true, string? dsOwner = null,
            string? rrsigOwner = null, bool includeUnrelatedKey = false, bool includeUnsignedDs = false,
            DateTime? inception = null, DateTime? expiration = null, byte dsDigestType = 2,
            ushort keyFlags = 257, byte keyProtocol = 3) {
            const string owner = "example.com.";
            using RSA rsa = RSA.Create(1024);
            byte[] publicKey = BuildPublicKey(rsa.ExportParameters(true));
            string publicKeyBase64 = Convert.ToBase64String(publicKey);
            ushort tag = ComputeKeyTag(keyFlags, keyProtocol, DnsKeyAlgorithm.RSASHA256, publicKey);
            DateTime starts = inception ?? DateTime.UtcNow.AddMinutes(-1);
            DateTime ends = expiration ?? DateTime.UtcNow.AddHours(1);
            byte[] signedData = BuildSignedData(owner, 3600, ends, starts, tag, publicKey, keyFlags, keyProtocol);
            byte[] signature = rsa.SignData(signedData, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var answers = new List<DnsAnswer> {
                new DnsAnswer { Name = owner, Type = DnsRecordType.DNSKEY, TTL = 3600,
                    DataRaw = $"{keyFlags} {keyProtocol} 8 {publicKeyBase64}" },
                new DnsAnswer { Name = rrsigOwner ?? owner, Type = DnsRecordType.RRSIG, TTL = 3600,
                    DataRaw = $"DNSKEY 8 2 3600 {(uint)(ends - new DateTime(1970, 1, 1)).TotalSeconds} {(uint)(starts - new DateTime(1970, 1, 1)).TotalSeconds} {tag} {owner} {Convert.ToBase64String(signature)}" }
            };

            if (includeDs) {
                string dsName = dsOwner ?? owner;
                answers.Add(new DnsAnswer { Name = dsName, Type = DnsRecordType.DS, TTL = 3600,
                    DataRaw = $"{tag} 8 {dsDigestType} {ComputeDigest(dsName, keyFlags, keyProtocol, DnsKeyAlgorithm.RSASHA256, publicKey, dsDigestType)}" });
            }

            if (includeUnrelatedKey) {
                answers.Add(new DnsAnswer { Name = "unrelated.example.", Type = DnsRecordType.DNSKEY, TTL = 3600,
                    DataRaw = $"257 3 8 {publicKeyBase64}" });
                if (includeUnsignedDs) {
                    ushort unrelatedTag = ComputeKeyTag(257, 3, DnsKeyAlgorithm.RSASHA256, publicKey);
                    answers.Add(new DnsAnswer { Name = "unrelated.example.", Type = DnsRecordType.DS, TTL = 3600,
                        DataRaw = $"{unrelatedTag} 8 2 {ComputeDigest("unrelated.example.", 257, 3, DnsKeyAlgorithm.RSASHA256, publicKey)}" });
                }
            }

            return new DnsResponse { Answers = answers.ToArray() };
        }

        private static byte[] BuildPublicKey(RSAParameters p) {
            byte[] exponent = p.Exponent!;
            byte[] modulus = p.Modulus!;
            var key = new byte[(exponent.Length > 255 ? 3 : 1) + exponent.Length + modulus.Length];
            int index = 0;
            if (exponent.Length > 255) {
                key[index++] = 0;
                key[index++] = (byte)(exponent.Length >> 8);
                key[index++] = (byte)exponent.Length;
            } else {
                key[index++] = (byte)exponent.Length;
            }
            Buffer.BlockCopy(exponent, 0, key, index, exponent.Length);
            index += exponent.Length;
            Buffer.BlockCopy(modulus, 0, key, index, modulus.Length);
            return key;
        }
    }
}
