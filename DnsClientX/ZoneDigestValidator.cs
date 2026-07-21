using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DnsClientX {
    /// <summary>Validates RFC 8976 SIMPLE-SCHEME ZONEMD records over an AXFR result.</summary>
    public static class ZoneDigestValidator {
        /// <summary>
        /// Validates SHA-384 or SHA-512 apex ZONEMD records against a complete wire-preserving transfer.
        /// A valid digest proves integrity only; authenticate the ZONEMD RRset separately with DNSSEC or a trusted channel.
        /// </summary>
        public static ZoneDigestValidationResult Validate(string zone, IEnumerable<ZoneTransferResult> transfer) {
            if (string.IsNullOrWhiteSpace(zone)) throw new ArgumentNullException(nameof(zone));
            if (transfer == null) throw new ArgumentNullException(nameof(transfer));
            string apex = DnsWireNameCodec.Canonical(zone);
            ZoneTransferResult[] chunks = transfer.ToArray();
            if (chunks.Length == 0) {
                return Invalid("The zone transfer is empty.");
            }
            if (chunks.Any(chunk => chunk.CanonicalRecords == null ||
                                    chunk.CanonicalRecords.Length != chunk.Records.Length)) {
                return Invalid("ZONEMD validation requires a complete wire-preserving result returned by ClientX.ZoneTransferAsync.");
            }

            ZoneCanonicalRecord[] records = chunks.SelectMany(chunk => chunk.CanonicalRecords).ToArray();
            ZoneCanonicalRecord[] soaRecords = records.Where(record =>
                record.Type == DnsRecordType.SOA && string.Equals(record.Name, apex, StringComparison.Ordinal)).ToArray();
            if (soaRecords.Length == 0 || !TryReadSoaSerial(soaRecords[0].CanonicalRdata, out uint soaSerial)) {
                return Invalid("The transferred zone does not contain a readable apex SOA record.");
            }

            ZoneCanonicalRecord[] zoneMdRecords = records.Where(record =>
                record.Type == DnsRecordType.ZONEMD && string.Equals(record.Name, apex, StringComparison.Ordinal)).ToArray();
            if (zoneMdRecords.Length == 0) {
                return new ZoneDigestValidationResult(ZoneDigestValidationStatus.Missing,
                    "The transferred zone does not publish an apex ZONEMD record.", soaSerial);
            }

            var parsed = new List<ZoneDigestRecord>();
            foreach (ZoneCanonicalRecord record in zoneMdRecords) {
                if (!TryParse(record.CanonicalRdata, out ZoneDigestRecord value, out string error)) {
                    return Invalid(error, soaSerial);
                }
                parsed.Add(value);
            }

            var duplicateTuples = new HashSet<int>(parsed.GroupBy(value => (value.Scheme, value.HashAlgorithm))
                .Where(group => group.Count() > 1)
                .Select(group => TupleKey(group.Key.Scheme, group.Key.HashAlgorithm)));
            bool sawSupported = false;
            bool sawSerialMatch = false;
            foreach (ZoneDigestRecord candidate in parsed) {
                if (candidate.Scheme != 1 || (candidate.HashAlgorithm != 1 && candidate.HashAlgorithm != 2)) continue;
                sawSupported = true;
                if (candidate.Serial != soaSerial) continue;
                sawSerialMatch = true;
                if (duplicateTuples.Contains(TupleKey(candidate.Scheme, candidate.HashAlgorithm))) continue;
                int expectedLength = candidate.HashAlgorithm == 1 ? 48 : 64;
                if (candidate.Digest.Length != expectedLength) continue;
                byte[] calculated = ComputeDigest(apex, records, candidate.HashAlgorithm);
                if (FixedTimeEquals(calculated, candidate.Digest)) {
                    return new ZoneDigestValidationResult(ZoneDigestValidationStatus.Valid,
                        "The RFC 8976 SIMPLE-SCHEME zone digest matches. This checksum does not by itself authenticate the zone origin.",
                        soaSerial, candidate.Scheme, candidate.HashAlgorithm);
                }
            }

            if (!sawSupported) {
                return new ZoneDigestValidationResult(ZoneDigestValidationStatus.Unsupported,
                    "No apex ZONEMD record uses the supported SIMPLE scheme with SHA-384 or SHA-512.", soaSerial);
            }
            if (!sawSerialMatch) return Invalid("No supported ZONEMD record has the apex SOA serial.", soaSerial);
            return Invalid("No unique, supported ZONEMD record matches the transferred zone.", soaSerial);
        }

        internal static byte[] ComputeDigest(string apex, IEnumerable<ZoneCanonicalRecord> source, byte hashAlgorithm) {
            ZoneCanonicalRecord[] records = source
                .Where(record => IsWithinZone(record.Name, apex) && !ExcludedFromDigest(apex, record))
                .GroupBy(record => Convert.ToBase64String(record.CanonicalWire), StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(record => record, ZoneCanonicalRecordComparer.Instance)
                .ToArray();
            using HashAlgorithm hash = hashAlgorithm switch {
                1 => SHA384.Create(),
                2 => SHA512.Create(),
                _ => throw new NotSupportedException($"ZONEMD hash algorithm {hashAlgorithm} is not supported.")
            };
            foreach (ZoneCanonicalRecord record in records) {
                hash.TransformBlock(record.CanonicalWire, 0, record.CanonicalWire.Length, null, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return hash.Hash ?? throw new CryptographicException("The zone digest could not be finalized.");
        }

        private static bool ExcludedFromDigest(string apex, ZoneCanonicalRecord record) {
            if (!string.Equals(record.Name, apex, StringComparison.Ordinal)) return false;
            if (record.Type == DnsRecordType.ZONEMD) return true;
            return record.Type == DnsRecordType.RRSIG && record.CanonicalRdata.Length >= 2 &&
                   ReadUInt16(record.CanonicalRdata, 0) == (ushort)DnsRecordType.ZONEMD;
        }

        private static bool IsWithinZone(string name, string apex) {
            string canonicalName = DnsWireNameCodec.Canonical(name);
            if (apex == ".") return true;
            return string.Equals(canonicalName, apex, StringComparison.Ordinal)
                || (canonicalName.Length > apex.Length
                    && canonicalName.EndsWith(apex, StringComparison.Ordinal)
                    && canonicalName[canonicalName.Length - apex.Length - 1] == '.');
        }

        private static bool TryReadSoaSerial(byte[] rdata, out uint serial) {
            serial = 0;
            try {
                int offset = SkipName(rdata, 0);
                offset = SkipName(rdata, offset);
                if (offset > rdata.Length - 20) return false;
                serial = ReadUInt32(rdata, offset);
                return true;
            } catch (DnsClientException) {
                return false;
            }
        }

        private static bool TryParse(byte[] rdata, out ZoneDigestRecord record, out string error) {
            record = default;
            error = string.Empty;
            if (rdata.Length < 18) {
                error = "An apex ZONEMD record is shorter than the RFC 8976 minimum digest size.";
                return false;
            }
            uint serial = ReadUInt32(rdata, 0);
            byte scheme = rdata[4];
            byte hashAlgorithm = rdata[5];
            byte[] digest = new byte[rdata.Length - 6];
            Buffer.BlockCopy(rdata, 6, digest, 0, digest.Length);
            record = new ZoneDigestRecord(serial, scheme, hashAlgorithm, digest);
            return true;
        }

        private static int SkipName(byte[] value, int offset) {
            while (true) {
                if (offset >= value.Length) throw new DnsClientException("A canonical DNS name is truncated.");
                int length = value[offset++];
                if ((length & 0xC0) != 0 || length > 63 || offset > value.Length - length) {
                    throw new DnsClientException("A canonical DNS name is malformed.");
                }
                if (length == 0) return offset;
                offset += length;
            }
        }

        private static ushort ReadUInt16(byte[] value, int offset) =>
            (ushort)((value[offset] << 8) | value[offset + 1]);

        private static uint ReadUInt32(byte[] value, int offset) =>
            ((uint)value[offset] << 24) | ((uint)value[offset + 1] << 16) |
            ((uint)value[offset + 2] << 8) | value[offset + 3];

        private static int TupleKey(byte scheme, byte hashAlgorithm) => (scheme << 8) | hashAlgorithm;

        private static bool FixedTimeEquals(byte[] left, byte[] right) {
            if (left.Length != right.Length) return false;
            int difference = 0;
            for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static ZoneDigestValidationResult Invalid(string message, uint? serial = null) =>
            new(ZoneDigestValidationStatus.Invalid, message, serial);

        private readonly struct ZoneDigestRecord {
            internal ZoneDigestRecord(uint serial, byte scheme, byte hashAlgorithm, byte[] digest) {
                Serial = serial;
                Scheme = scheme;
                HashAlgorithm = hashAlgorithm;
                Digest = digest;
            }
            internal uint Serial { get; }
            internal byte Scheme { get; }
            internal byte HashAlgorithm { get; }
            internal byte[] Digest { get; }
        }

        private sealed class ZoneCanonicalRecordComparer : IComparer<ZoneCanonicalRecord> {
            internal static readonly ZoneCanonicalRecordComparer Instance = new();
            public int Compare(ZoneCanonicalRecord left, ZoneCanonicalRecord right) {
                int result = CompareNames(left.Name, right.Name);
                if (result != 0) return result;
                result = ((ushort)left.Type).CompareTo((ushort)right.Type);
                if (result != 0) return result;
                result = left.Class.CompareTo(right.Class);
                if (result != 0) return result;
                return CompareBytes(left.CanonicalRdata, right.CanonicalRdata);
            }

            private static int CompareNames(string left, string right) {
                byte[][] leftLabels = Labels(DnsWireNameCodec.ToCanonicalWire(left));
                byte[][] rightLabels = Labels(DnsWireNameCodec.ToCanonicalWire(right));
                int shared = Math.Min(leftLabels.Length, rightLabels.Length);
                for (int i = 1; i <= shared; i++) {
                    int result = CompareBytes(leftLabels[leftLabels.Length - i], rightLabels[rightLabels.Length - i]);
                    if (result != 0) return result;
                }
                return leftLabels.Length.CompareTo(rightLabels.Length);
            }

            private static byte[][] Labels(byte[] wire) {
                var labels = new List<byte[]>();
                int offset = 0;
                while (offset < wire.Length && wire[offset] != 0) {
                    int length = wire[offset++];
                    var label = new byte[length];
                    Buffer.BlockCopy(wire, offset, label, 0, length);
                    labels.Add(label);
                    offset += length;
                }
                return labels.ToArray();
            }

            private static int CompareBytes(byte[] left, byte[] right) {
                int count = Math.Min(left.Length, right.Length);
                for (int i = 0; i < count; i++) {
                    int result = left[i].CompareTo(right[i]);
                    if (result != 0) return result;
                }
                return left.Length.CompareTo(right.Length);
            }
        }
    }
}
