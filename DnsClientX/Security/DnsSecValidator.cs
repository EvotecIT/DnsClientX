using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// Provides helper methods for validating DNSSEC responses against the built-in root trust anchors.
    /// </summary>
    public static class DnsSecValidator {
        private readonly struct DnsKeyRecord {
            public string Name { get; }
            public ushort Flags { get; }
            public byte Protocol { get; }
            public DnsKeyAlgorithm Algorithm { get; }
            public byte[] PublicKey { get; }

            public DnsKeyRecord(string name, ushort flags, byte protocol, DnsKeyAlgorithm algorithm, byte[] publicKey) {
                Name = name;
                Flags = flags;
                Protocol = protocol;
                Algorithm = algorithm;
                PublicKey = publicKey;
            }
        }

        private readonly struct DsRecord {
            public string Name { get; }
            public ushort KeyTag { get; }
            public DnsKeyAlgorithm Algorithm { get; }
            public byte DigestType { get; }
            public string Digest { get; }

            public DsRecord(string name, ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest) {
                Name = name;
                KeyTag = keyTag;
                Algorithm = algorithm;
                DigestType = digestType;
                Digest = digest;
            }
        }

        private readonly struct RrsigRecord {
            public DnsRecordType TypeCovered { get; }
            public DnsKeyAlgorithm Algorithm { get; }
            public byte Labels { get; }
            public int OriginalTtl { get; }
            public DateTime Expiration { get; }
            public DateTime Inception { get; }
            public ushort KeyTag { get; }
            public string SignerName { get; }
            public byte[] Signature { get; }

            public RrsigRecord(DnsRecordType typeCovered, DnsKeyAlgorithm algorithm, byte labels, int originalTtl, DateTime expiration, DateTime inception, ushort keyTag, string signerName, byte[] signature) {
                TypeCovered = typeCovered;
                Algorithm = algorithm;
                Labels = labels;
                OriginalTtl = originalTtl;
                Expiration = expiration;
                Inception = inception;
                KeyTag = keyTag;
                SignerName = signerName;
                Signature = signature;
            }
        }
        /// <summary>
        /// Validates the supplied <see cref="DnsResponse"/> against known root DS records.
        /// </summary>
        /// <param name="response">DNS response to validate.</param>
        /// <returns><c>true</c> if the response can be validated; otherwise <c>false</c>.</returns>
        public static bool ValidateAgainstRoot(DnsResponse response) {
            if (response.Answers == null) {
                return false;
            }

            foreach (DnsAnswer answer in response.Answers) {
                if (answer.Type == DnsRecordType.DS) {
                    if (TryParseDs(answer.DataRaw, out RootDsRecord ds) &&
                        RootTrustAnchors.DsRecords.Any(r => r.KeyTag == ds.KeyTag && r.Algorithm == ds.Algorithm && r.DigestType == ds.DigestType && string.Equals(r.Digest, ds.Digest, StringComparison.OrdinalIgnoreCase))) {
                        return true;
                    }
                } else if (answer.Type == DnsRecordType.DNSKEY) {
                    if (TryParseDnsKey(answer, out ushort flags, out byte protocol, out DnsKeyAlgorithm algorithm, out byte[] publicKey)) {
                        ushort keyTag = ComputeKeyTag(flags, protocol, algorithm, publicKey);
                        string digest = ComputeDigest(answer.Name, flags, protocol, algorithm, publicKey);
                        if (RootTrustAnchors.DsRecords.Any(r => r.KeyTag == keyTag && r.Algorithm == algorithm && r.DigestType == 2 && string.Equals(r.Digest, digest, StringComparison.OrdinalIgnoreCase))) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Validates DNSSEC data by verifying DS records and RRSIG signatures for DNSKEY sets.
        /// </summary>
        /// <param name="response">DNS response to validate.</param>
        /// <returns><c>true</c> when validation succeeds; otherwise <c>false</c>.</returns>
        public static bool ValidateChain(DnsResponse response) {
            if (response.Answers == null) {
                return false;
            }

            var dnsKeys = new List<DnsKeyRecord>();
            var dsRecords = new List<DsRecord>();
            var rrsigs = new List<RrsigRecord>();

            foreach (DnsAnswer answer in response.Answers) {
                if (answer.Type == DnsRecordType.DNSKEY) {
                    if (TryParseDnsKey(answer, out ushort flags, out byte protocol, out DnsKeyAlgorithm algorithm, out byte[] publicKey)) {
                        dnsKeys.Add(new DnsKeyRecord(answer.Name, flags, protocol, algorithm, publicKey));
                    }
                } else if (answer.Type == DnsRecordType.DS) {
                    if (TryParseDs(answer.DataRaw, out RootDsRecord ds)) {
                        dsRecords.Add(new DsRecord(answer.Name, ds.KeyTag, ds.Algorithm, ds.DigestType, ds.Digest));
                    }
                } else if (answer.Type == DnsRecordType.RRSIG) {
                    if (TryParseRrsig(answer, out RrsigRecord sig)) {
                        rrsigs.Add(sig);
                    }
                }
            }

            if (dnsKeys.Count == 0 || rrsigs.Count == 0) {
                return false;
            }

            foreach (RrsigRecord sig in rrsigs.Where(s => s.TypeCovered == DnsRecordType.DNSKEY)) {
                if (!VerifyDnskeyRrsig(sig, dnsKeys)) {
                    return false;
                }
            }

            foreach (DsRecord ds in dsRecords) {
                DnsKeyRecord? key = dnsKeys.FirstOrDefault(k => ComputeKeyTag(k.Flags, k.Protocol, k.Algorithm, k.PublicKey) == ds.KeyTag && k.Algorithm == ds.Algorithm);
                if (key == null) {
                    return false;
                }
                string digest = ComputeDigest(ds.Name, key.Value.Flags, key.Value.Protocol, key.Value.Algorithm, key.Value.PublicKey);
                if (!digest.Equals(ds.Digest, StringComparison.OrdinalIgnoreCase)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to parse a DS record string into a <see cref="RootDsRecord"/> instance.
        /// </summary>
        /// <param name="dataRaw">Raw DS record data.</param>
        /// <param name="record">Parsed record when the method returns <c>true</c>.</param>
        /// <returns><c>true</c> when parsing succeeds; otherwise <c>false</c>.</returns>
        private static bool TryParseDs(string dataRaw, out RootDsRecord record) {
            record = default;
            if (string.IsNullOrWhiteSpace(dataRaw)) {
                return false;
            }
            string[] parts = dataRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) {
                return false;
            }
            if (!ushort.TryParse(parts[0], out ushort keyTag)) {
                return false;
            }
            DnsKeyAlgorithm parsedAlgorithm;
            if (Enum.TryParse(parts[1], true, out DnsKeyAlgorithm algEnum)) {
                parsedAlgorithm = algEnum;
            } else if (byte.TryParse(parts[1], out byte algVal) &&
                       Enum.IsDefined(typeof(DnsKeyAlgorithm), (int)algVal)) {
                parsedAlgorithm = (DnsKeyAlgorithm)algVal;
            } else {
                return false;
            }
            if (!byte.TryParse(parts[2], out byte digestType)) {
                return false;
            }
            record = new RootDsRecord(keyTag, parsedAlgorithm, digestType, parts[3].ToUpperInvariant());
            return true;
        }

        /// <summary>
        /// Attempts to parse a DNSKEY record into its individual components.
        /// </summary>
        /// <param name="answer">DNS answer containing the DNSKEY data.</param>
        /// <param name="flags">Parsed key flags.</param>
        /// <param name="protocol">DNS protocol value.</param>
        /// <param name="algorithm">Key algorithm.</param>
        /// <param name="publicKey">Extracted public key bytes.</param>
        /// <returns><c>true</c> when parsing succeeds; otherwise <c>false</c>.</returns>
        private static bool TryParseDnsKey(DnsAnswer answer, out ushort flags, out byte protocol, out DnsKeyAlgorithm algorithm, out byte[] publicKey) {
            flags = 0;
            protocol = 0;
            algorithm = 0;
            publicKey = Array.Empty<byte>();
            string dataRaw = answer.DataRaw;
            if (string.IsNullOrWhiteSpace(dataRaw)) {
                return false;
            }
            string[] parts = dataRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) {
                return false;
            }
            if (!ushort.TryParse(parts[0], out flags)) {
                return false;
            }
            if (!byte.TryParse(parts[1], out protocol)) {
                return false;
            }
            if (Enum.TryParse(parts[2], true, out DnsKeyAlgorithm algEnum)) {
                algorithm = algEnum;
            } else if (byte.TryParse(parts[2], out byte algVal) &&
                       Enum.IsDefined(typeof(DnsKeyAlgorithm), (int)algVal)) {
                algorithm = (DnsKeyAlgorithm)algVal;
            } else {
                return false;
            }
            string keyBase64 = string.Concat(parts.Skip(3));
            try {
                publicKey = Convert.FromBase64String(keyBase64);
            } catch {
                return false;
            }
            return true;
        }

        private static bool TryParseRrsig(DnsAnswer answer, out RrsigRecord record) {
            record = default;
            if (string.IsNullOrWhiteSpace(answer.DataRaw)) {
                return false;
            }

            string[] parts = answer.DataRaw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9) {
                return false;
            }

            if (!Enum.TryParse(parts[0], true, out DnsRecordType typeCovered)) {
                if (ushort.TryParse(parts[0], out ushort typeVal)) {
                    typeCovered = (DnsRecordType)typeVal;
                } else {
                    return false;
                }
            }

            if (!Enum.TryParse(parts[1], true, out DnsKeyAlgorithm alg)) {
                if (byte.TryParse(parts[1], out byte algVal) && Enum.IsDefined(typeof(DnsKeyAlgorithm), (int)algVal)) {
                    alg = (DnsKeyAlgorithm)algVal;
                } else {
                    return false;
                }
            }

            if (!byte.TryParse(parts[2], out byte labels)) {
                return false;
            }

            if (!int.TryParse(parts[3], out int originalTtl)) {
                return false;
            }

            if (!uint.TryParse(parts[4], out uint expirationUnix)) {
                return false;
            }
            if (!uint.TryParse(parts[5], out uint inceptionUnix)) {
                return false;
            }

            if (!ushort.TryParse(parts[6], out ushort keyTag)) {
                return false;
            }

            string signerName = parts[7];
            string sigBase64 = string.Concat(parts.Skip(8));
            try {
                byte[] sig = Convert.FromBase64String(sigBase64);
                record = new RrsigRecord(typeCovered, alg, labels, originalTtl, UnixToDateTime(expirationUnix), UnixToDateTime(inceptionUnix), keyTag, signerName, sig);
                return true;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Computes the key tag value for a DNSKEY record as defined in RFC 4034.
        /// </summary>
        /// <param name="flags">DNSKEY flags.</param>
        /// <param name="protocol">Protocol value.</param>
        /// <param name="algorithm">Algorithm identifier.</param>
        /// <param name="publicKey">Public key bytes.</param>
        /// <returns>The calculated key tag.</returns>
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

        /// <summary>
        /// Computes a SHA-256 digest for the provided DNSKEY parameters.
        /// </summary>
        /// <param name="name">Domain name associated with the key.</param>
        /// <param name="flags">DNSKEY flags.</param>
        /// <param name="protocol">Protocol value.</param>
        /// <param name="algorithm">Algorithm identifier.</param>
        /// <param name="publicKey">Public key bytes.</param>
        /// <returns>Calculated digest in hexadecimal form.</returns>
        private static string ComputeDigest(string name, ushort flags, byte protocol, DnsKeyAlgorithm algorithm, byte[] publicKey) {
            byte[] owner = DomainToWireFormat(name);
            byte[] rdata = new byte[4 + publicKey.Length];
            BinaryPrimitives.WriteUInt16BigEndian(rdata, flags);
            rdata[2] = protocol;
            rdata[3] = (byte)algorithm;
            Buffer.BlockCopy(publicKey, 0, rdata, 4, publicKey.Length);
            byte[] message = new byte[owner.Length + rdata.Length];
            Buffer.BlockCopy(owner, 0, message, 0, owner.Length);
            Buffer.BlockCopy(rdata, 0, message, owner.Length, rdata.Length);
            using SHA256 sha256 = SHA256.Create();
            byte[] digestBytes = sha256.ComputeHash(message);
            return BitConverter.ToString(digestBytes).Replace("-", string.Empty).ToUpperInvariant();
        }

        private static byte[] BuildDnskeySignedData(RrsigRecord rrsig, IReadOnlyCollection<DnsKeyRecord> dnsKeys) {
            byte[] signerName = DomainToWireFormat(rrsig.SignerName);
            byte[] header = new byte[18 + signerName.Length];
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(0), (ushort)rrsig.TypeCovered);
            header[2] = (byte)rrsig.Algorithm;
            header[3] = rrsig.Labels;
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)rrsig.OriginalTtl);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), (uint)(rrsig.Expiration.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), (uint)(rrsig.Inception.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds);
            BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(16), rrsig.KeyTag);
            Buffer.BlockCopy(signerName, 0, header, 18, signerName.Length);

            var rrBytes = new List<byte[]>();
            foreach (DnsKeyRecord key in dnsKeys) {
                byte[] owner = DomainToWireFormat(key.Name);
                byte[] rdata = new byte[4 + key.PublicKey.Length];
                BinaryPrimitives.WriteUInt16BigEndian(rdata, key.Flags);
                rdata[2] = key.Protocol;
                rdata[3] = (byte)key.Algorithm;
                Buffer.BlockCopy(key.PublicKey, 0, rdata, 4, key.PublicKey.Length);
                byte[] rr = new byte[owner.Length + 10 + rdata.Length];
                int pos = 0;
                Buffer.BlockCopy(owner, 0, rr, pos, owner.Length);
                pos += owner.Length;
                BinaryPrimitives.WriteUInt16BigEndian(rr.AsSpan(pos), (ushort)DnsRecordType.DNSKEY);
                pos += 2;
                BinaryPrimitives.WriteUInt16BigEndian(rr.AsSpan(pos), 1);
                pos += 2;
                BinaryPrimitives.WriteUInt32BigEndian(rr.AsSpan(pos), (uint)rrsig.OriginalTtl);
                pos += 4;
                BinaryPrimitives.WriteUInt16BigEndian(rr.AsSpan(pos), (ushort)rdata.Length);
                pos += 2;
                Buffer.BlockCopy(rdata, 0, rr, pos, rdata.Length);
                rrBytes.Add(rr);
            }

            rrBytes.Sort(ByteArrayComparer.Instance);

            int totalLength = header.Length + rrBytes.Sum(r => r.Length);
            byte[] data = new byte[totalLength];
            Buffer.BlockCopy(header, 0, data, 0, header.Length);
            int offset = header.Length;
            foreach (byte[] rr in rrBytes) {
                Buffer.BlockCopy(rr, 0, data, offset, rr.Length);
                offset += rr.Length;
            }

            return data;
        }

        private static bool VerifyDnskeyRrsig(RrsigRecord rrsig, IReadOnlyCollection<DnsKeyRecord> dnsKeys) {
            foreach (DnsKeyRecord key in dnsKeys) {
                ushort tag = ComputeKeyTag(key.Flags, key.Protocol, key.Algorithm, key.PublicKey);
                if (tag != rrsig.KeyTag || key.Algorithm != rrsig.Algorithm) {
                    continue;
                }

                byte[] data = BuildDnskeySignedData(rrsig, dnsKeys);

                if (key.Algorithm == DnsKeyAlgorithm.RSASHA256 || key.Algorithm == DnsKeyAlgorithm.RSASHA512) {
                    if (TryGetRsaParameters(key.PublicKey, out RSAParameters p)) {
                        using RSA rsa = RSA.Create();
                        rsa.ImportParameters(p);
                        HashAlgorithmName hashAlg = key.Algorithm == DnsKeyAlgorithm.RSASHA256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512;
                        if (rsa.VerifyData(data, rrsig.Signature, hashAlg, RSASignaturePadding.Pkcs1)) {
                            return true;
                        }
                    }
                }
#if NET5_0_OR_GREATER
                else if (key.Algorithm == DnsKeyAlgorithm.ECDSAP256SHA256 || key.Algorithm == DnsKeyAlgorithm.ECDSAP384SHA384) {
                    if (TryGetEcdsa(key.PublicKey, key.Algorithm, out ECDsa? ecdsa)) {
                        using (ecdsa) {
                            HashAlgorithmName hashAlg = key.Algorithm == DnsKeyAlgorithm.ECDSAP256SHA256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA384;
                            if (ecdsa.VerifyData(data, rrsig.Signature, hashAlg)) {
                                return true;
                            }
                        }
                    }
                }
#endif
            }

            return false;
        }

        private static bool TryGetRsaParameters(byte[] keyData, out RSAParameters parameters) {
            parameters = default;
            try {
                int index = 0;
                int exponentLength = keyData[index++];
                if (exponentLength == 0) {
                    exponentLength = (keyData[index] << 8) | keyData[index + 1];
                    index += 2;
                }
                byte[] exponent = new byte[exponentLength];
                Buffer.BlockCopy(keyData, index, exponent, 0, exponentLength);
                index += exponentLength;
                byte[] modulus = new byte[keyData.Length - index];
                Buffer.BlockCopy(keyData, index, modulus, 0, modulus.Length);
                parameters = new RSAParameters { Exponent = exponent, Modulus = modulus };
                return true;
            } catch {
                return false;
            }
        }

#if NET5_0_OR_GREATER
        private static bool TryGetEcdsa(byte[] keyData, DnsKeyAlgorithm algorithm, out ECDsa? ecdsa) {
            ecdsa = null;
            try {
                ECParameters ec = new();
                ec.Curve = algorithm == DnsKeyAlgorithm.ECDSAP384SHA384 ? ECCurve.NamedCurves.nistP384 : ECCurve.NamedCurves.nistP256;
                int coordLength = keyData.Length / 2;
                ec.Q = new ECPoint {
                    X = keyData.AsSpan(0, coordLength).ToArray(),
                    Y = keyData.AsSpan(coordLength).ToArray()
                };
                ecdsa = ECDsa.Create(ec);
                return true;
            } catch {
                return false;
            }
        }
#endif

        private sealed class ByteArrayComparer : IComparer<byte[]> {
            internal static readonly ByteArrayComparer Instance = new();

            public int Compare(byte[]? x, byte[]? y) {
                if (x is null && y is null) return 0;
                if (x is null) return -1;
                if (y is null) return 1;
                int len = Math.Min(x.Length, y.Length);
                for (int i = 0; i < len; i++) {
                    int cmp = x[i].CompareTo(y[i]);
                    if (cmp != 0) return cmp;
                }
                return x.Length.CompareTo(y.Length);
            }
        }

        /// <summary>
        /// Converts a domain name to its DNS wire format representation.
        /// </summary>
        /// <param name="domain">Domain name to convert.</param>
        /// <returns>Byte array containing the wire format.</returns>
        private static byte[] DomainToWireFormat(string domain) {
            if (string.IsNullOrEmpty(domain) || domain == ".") {
                return new byte[] { 0 };
            }
            string[] labels = domain.TrimEnd('.').Split('.');
            var data = new List<byte>();
            foreach (string label in labels) {
                byte[] bytes = Encoding.ASCII.GetBytes(label.ToLowerInvariant());
                data.Add((byte)bytes.Length);
                data.AddRange(bytes);
            }
            data.Add(0);
            return data.ToArray();
        }

        private static DateTime UnixToDateTime(uint seconds) {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
        }
    }
}
