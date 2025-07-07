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
    }
}
