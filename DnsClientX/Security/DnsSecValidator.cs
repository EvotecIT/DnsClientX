using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace DnsClientX {
    /// <summary>
    /// Provides compatibility helpers for inspecting DNSKEY, DS, and DNSKEY RRSIG material already
    /// present in a response. For end-to-end DNSSEC answer and denial validation, use
    /// <see cref="ClientX.Resolve(string,DnsRecordType,bool,bool,bool,bool,int,int,bool,bool,System.Threading.CancellationToken)"/>
    /// with <c>validateDnsSec: true</c> and inspect <see cref="DnsResponse.DnsSecValidationStatus"/>.
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
            public string Name { get; }
            public DnsRecordType TypeCovered { get; }
            public DnsKeyAlgorithm Algorithm { get; }
            public byte Labels { get; }
            public int OriginalTtl { get; }
            public DateTime Expiration { get; }
            public DateTime Inception { get; }
            public ushort KeyTag { get; }
            public string SignerName { get; }
            public byte[] Signature { get; }

            public RrsigRecord(string name, DnsRecordType typeCovered, DnsKeyAlgorithm algorithm, byte labels, int originalTtl, DateTime expiration, DateTime inception, ushort keyTag, string signerName, byte[] signature) {
                Name = name;
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

        /// <summary>Computes the RFC 4034 key tag for DNSKEY presentation-format RDATA.</summary>
        /// <param name="dnskeyRecord">DNSKEY RDATA in the form flags, protocol, algorithm, and Base64 key.</param>
        /// <returns>The key tag, or zero when the value cannot be parsed.</returns>
        public static ushort ComputeKeyTag(string dnskeyRecord) {
            if (string.IsNullOrWhiteSpace(dnskeyRecord)) return 0;
            string[] parts = dnskeyRecord.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || !ushort.TryParse(parts[0], out ushort flags) ||
                !byte.TryParse(parts[1], out byte protocol)) return 0;

            DnsKeyAlgorithm algorithm;
            if (Enum.TryParse(parts[2], true, out DnsKeyAlgorithm named)) {
                algorithm = named;
            } else if (byte.TryParse(parts[2], out byte numeric) &&
                       Enum.IsDefined(typeof(DnsKeyAlgorithm), (int)numeric)) {
                algorithm = (DnsKeyAlgorithm)numeric;
            } else {
                return 0;
            }

            try {
                return ComputeKeyTag(flags, protocol, algorithm, Convert.FromBase64String(parts[3]));
            } catch (FormatException) {
                return 0;
            }
        }
        /// <summary>
        /// Checks whether root-owned DNSKEY or DS material in the supplied response matches a
        /// currently valid bundled root trust anchor. This does not validate a DNS response or chain.
        /// </summary>
        /// <param name="response">DNS response to validate.</param>
        /// <returns><c>true</c> when root-owned material matches an active root anchor; otherwise <c>false</c>.</returns>
        public static bool ValidateAgainstRoot(DnsResponse response) => ValidateAgainstRoot(response, out _);

        /// <summary>
        /// Checks whether root-owned DNSKEY or DS material in the supplied response matches a
        /// currently valid bundled root trust anchor. This does not validate a DNS response or chain.
        /// </summary>
        /// <param name="response">DNS response to validate.</param>
        /// <param name="message">Detailed failure message when validation fails.</param>
        /// <returns><c>true</c> when root-owned material matches an active root anchor; otherwise <c>false</c>.</returns>
        public static bool ValidateAgainstRoot(DnsResponse response, out string message) {
            message = string.Empty;
            if (response.Answers == null) {
                message = "No answers to validate.";
                return false;
            }

            foreach (DnsAnswer answer in response.Answers) {
                if (answer.Type == DnsRecordType.DS) {
                    if (!TryCanonicalName(answer.Name, out string owner) || owner != ".") {
                        message = "Root trust anchor material must have the root owner.";
                        continue;
                    }
                    if (TryParseDs(answer.DataRaw, out RootDsRecord ds)) {
                        if (RootTrustAnchors.DsRecords.Any(r => r.IsValidAt(DateTimeOffset.UtcNow) && r.KeyTag == ds.KeyTag && r.Algorithm == ds.Algorithm && r.DigestType == ds.DigestType && string.Equals(r.Digest, ds.Digest, StringComparison.OrdinalIgnoreCase))) {
                            return true;
                        }
                        message = $"DS record {ds.KeyTag} did not match root anchors.";
                    } else {
                        message = $"Failed to parse DS record '{answer.DataRaw}'.";
                    }
                } else if (answer.Type == DnsRecordType.DNSKEY) {
                    if (!TryCanonicalName(answer.Name, out string owner) || owner != ".") {
                        message = "Root trust anchor material must have the root owner.";
                        continue;
                    }
                    if (TryParseDnsKey(answer, out ushort flags, out byte protocol, out DnsKeyAlgorithm algorithm, out byte[] publicKey)) {
                        ushort keyTag = ComputeKeyTag(flags, protocol, algorithm, publicKey);
                        string digest = ComputeDigest(answer.Name, flags, protocol, algorithm, publicKey);
                        if (RootTrustAnchors.DsRecords.Any(r => r.IsValidAt(DateTimeOffset.UtcNow) && r.KeyTag == keyTag && r.Algorithm == algorithm && r.DigestType == 2 && string.Equals(r.Digest, digest, StringComparison.OrdinalIgnoreCase))) {
                            return true;
                        }
                        message = $"DNSKEY record with tag {keyTag} did not match root anchors.";
                    } else {
                        message = $"Failed to parse DNSKEY record '{answer.DataRaw}'.";
                    }
                }
            }

            if (message.Length == 0) {
                message = "No matching root trust anchor found.";
            }

            return false;
        }

        /// <summary>
        /// Inspects DNSSEC key material by verifying DS records and RRSIG signatures for DNSKEY sets
        /// that are already present in the supplied response. This method does not fetch a chain of
        /// trust and does not validate arbitrary answer RRsets or authenticated denial proofs.
        /// </summary>
        /// <param name="response">DNS response to validate.</param>
        /// <returns><c>true</c> when at least one supplied DNSKEY RRset has a valid signature and
        /// every supplied DS matches a key in a verified RRset; this does not mean the answer is DNSSEC Secure.</returns>
        public static bool ValidateChain(DnsResponse response) => ValidateChain(response, out _);

        /// <summary>
        /// Inspects DNSSEC key material by verifying DS records and RRSIG signatures for DNSKEY sets
        /// that are already present in the supplied response. This method does not fetch a chain of
        /// trust and does not validate arbitrary answer RRsets or authenticated denial proofs.
        /// </summary>
        /// <param name="response">DNS response to validate.</param>
        /// <param name="message">Detailed failure message when validation fails.</param>
        /// <returns><c>true</c> when at least one supplied DNSKEY RRset has a valid signature and
        /// every supplied DS matches a key in a verified RRset; this does not mean the answer is DNSSEC Secure.</returns>
        public static bool ValidateChain(DnsResponse response, out string message) {
            message = string.Empty;
            if (response.Answers == null) {
                message = "No answers to validate.";
                return false;
            }

            var dnsKeys = new List<DnsKeyRecord>();
            var dsRecords = new List<DsRecord>();
            var rrsigs = new List<RrsigRecord>();

            foreach (DnsAnswer answer in response.Answers) {
                if (answer.Type == DnsRecordType.DNSKEY) {
                    if (TryParseDnsKey(answer, out ushort flags, out byte protocol, out DnsKeyAlgorithm algorithm, out byte[] publicKey)) {
                        dnsKeys.Add(new DnsKeyRecord(answer.Name, flags, protocol, algorithm, publicKey));
                    } else {
                        message = $"Failed to parse DNSKEY record '{answer.DataRaw}'.";
                        return false;
                    }
                } else if (answer.Type == DnsRecordType.DS) {
                    if (TryParseDs(answer.DataRaw, out RootDsRecord ds)) {
                        dsRecords.Add(new DsRecord(answer.Name, ds.KeyTag, ds.Algorithm, ds.DigestType, ds.Digest));
                    } else {
                        message = $"Failed to parse DS record '{answer.DataRaw}'.";
                        return false;
                    }
                } else if (answer.Type == DnsRecordType.RRSIG) {
                    if (TryParseRrsig(answer, out RrsigRecord sig)) {
                        rrsigs.Add(sig);
                    }
                }
            }

            if (dnsKeys.Count == 0 || rrsigs.Count == 0) {
                message = "Missing DNSKEY or RRSIG records.";
                return false;
            }

            RrsigRecord[] dnskeySignatures = rrsigs.Where(s => s.TypeCovered == DnsRecordType.DNSKEY).ToArray();
            if (dnskeySignatures.Length == 0) {
                message = "Missing DNSKEY-covering RRSIG record.";
                return false;
            }

            var verifiedOwners = new HashSet<string>(StringComparer.Ordinal);
            foreach (RrsigRecord sig in dnskeySignatures) {
                if (!TryCanonicalName(sig.Name, out string owner) ||
                    !TryCanonicalName(sig.SignerName, out string signer) || signer != owner ||
                    sig.Labels != DnsWireNameCodec.EncodeLabels(owner).Length ||
                    !SignatureTimeIsValid(sig)) {
                    continue;
                }

                DnsKeyRecord[] rrset = dnsKeys.Where(k =>
                    TryCanonicalName(k.Name, out string keyOwner) && keyOwner == owner).ToArray();
                if (rrset.Length > 0 && VerifyDnskeyRrsig(sig, rrset)) {
                    verifiedOwners.Add(owner);
                }
            }

            if (verifiedOwners.Count == 0) {
                message = "Invalid RRSIG for supplied DNSKEY records.";
                return false;
            }

            foreach (DsRecord ds in dsRecords) {
                if (!TryCanonicalName(ds.Name, out string owner)) {
                    message = "Invalid DS owner name.";
                    return false;
                }
                if (!verifiedOwners.Contains(owner)) {
                    message = $"No verified DNSKEY RRSIG found for DS owner {ds.Name}.";
                    return false;
                }

                DnsKeyRecord[] matchingKeys = dnsKeys.Where(k =>
                    TryCanonicalName(k.Name, out string keyOwner) && keyOwner == owner &&
                    k.Protocol == 3 && (k.Flags & 0x0100) != 0 &&
                    ComputeKeyTag(k.Flags, k.Protocol, k.Algorithm, k.PublicKey) == ds.KeyTag &&
                    k.Algorithm == ds.Algorithm).ToArray();

                if (matchingKeys.Length == 0) {
                    message = $"No DNSKEY found for DS tag {ds.KeyTag}.";
                    return false;
                }

                bool supportedDigest = false;
                bool digestMatches = false;
                foreach (DnsKeyRecord key in matchingKeys) {
                    var dnssecKey = new DnsSecKey(key.Name, key.Flags, key.Protocol, (byte)key.Algorithm, key.PublicKey);
                    if (!DnsSecCrypto.TryComputeDsDigest(key.Name, dnssecKey, ds.DigestType, out byte[] digest)) {
                        continue;
                    }
                    supportedDigest = true;
                    if (BitConverter.ToString(digest).Replace("-", string.Empty)
                        .Equals(ds.Digest, StringComparison.OrdinalIgnoreCase)) {
                        digestMatches = true;
                        break;
                    }
                }

                if (!supportedDigest) {
                    message = $"Unsupported DS digest type {ds.DigestType}.";
                    return false;
                }

                if (!digestMatches) {
                    message = $"Digest mismatch for DS tag {ds.KeyTag}.";
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
            string digest = string.Concat(parts.Skip(3));
            if (digest.Length == 0 || (digest.Length & 1) != 0 || !digest.All(Uri.IsHexDigit)) {
                return false;
            }
            int expectedLength = digestType switch {
                1 => 40,
                2 => 64,
                4 => 96,
                _ => 0
            };
            if (expectedLength != 0 && digest.Length != expectedLength) {
                return false;
            }
            record = new RootDsRecord(keyTag, parsedAlgorithm, digestType, digest.ToUpperInvariant());
            return true;
        }

        private static bool TryCanonicalName(string? name, out string canonical) {
            canonical = string.Empty;
            if (string.IsNullOrWhiteSpace(name)) {
                return false;
            }
            try {
                canonical = DnsWireNameCodec.Canonical(name!);
                return true;
            } catch (ArgumentException) {
                return false;
            }
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
                record = new RrsigRecord(answer.Name, typeCovered, alg, labels, originalTtl, UnixToDateTime(expirationUnix), UnixToDateTime(inceptionUnix), keyTag, signerName, sig);
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
            byte[] data = BuildDnskeySignedData(rrsig, dnsKeys);
            foreach (DnsKeyRecord key in dnsKeys) {
                ushort tag = ComputeKeyTag(key.Flags, key.Protocol, key.Algorithm, key.PublicKey);
                if (tag != rrsig.KeyTag || key.Algorithm != rrsig.Algorithm ||
                    key.Protocol != 3 || (key.Flags & 0x0100) == 0) {
                    continue;
                }

                var dnssecKey = new DnsSecKey(key.Name, key.Flags, key.Protocol, (byte)key.Algorithm, key.PublicKey);
                if (DnsSecCrypto.Verify(dnssecKey, data, rrsig.Signature)) {
                    return true;
                }
            }

            return false;
        }

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
            return DnsWireNameCodec.ToCanonicalWire(domain);
        }

        private static bool SignatureTimeIsValid(RrsigRecord signature) {
            uint inception = unchecked((uint)new DateTimeOffset(signature.Inception).ToUnixTimeSeconds());
            uint expiration = unchecked((uint)new DateTimeOffset(signature.Expiration).ToUnixTimeSeconds());
            return DnsSecWire.SignatureTimeIsValid(inception, expiration, DateTimeOffset.UtcNow);
        }

        private static DateTime UnixToDateTime(uint seconds) {
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
        }
    }
}
