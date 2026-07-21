using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DnsClientX {
    internal readonly struct DnsSecKey {
        internal DnsSecKey(string name, ushort flags, byte protocol, byte algorithm, byte[] publicKey) {
            Name = DnsWireNameCodec.Canonical(name);
            Flags = flags;
            Protocol = protocol;
            Algorithm = algorithm;
            PublicKey = publicKey;
        }

        internal string Name { get; }
        internal ushort Flags { get; }
        internal byte Protocol { get; }
        internal byte Algorithm { get; }
        internal byte[] PublicKey { get; }
        internal ushort KeyTag => DnsSecCrypto.ComputeKeyTag(Flags, Protocol, Algorithm, PublicKey);
    }

    internal readonly struct DnsSecSignature {
        internal DnsSecSignature(string owner, ushort recordClass, DnsRecordType typeCovered, byte algorithm,
            byte labels, uint originalTtl, uint expiration, uint inception, ushort keyTag,
            string signerName, byte[] signature) {
            Owner = DnsWireNameCodec.Canonical(owner);
            RecordClass = recordClass;
            TypeCovered = typeCovered;
            Algorithm = algorithm;
            Labels = labels;
            OriginalTtl = originalTtl;
            Expiration = expiration;
            Inception = inception;
            KeyTag = keyTag;
            SignerName = DnsWireNameCodec.Canonical(signerName);
            Signature = signature;
        }

        internal string Owner { get; }
        internal ushort RecordClass { get; }
        internal DnsRecordType TypeCovered { get; }
        internal byte Algorithm { get; }
        internal byte Labels { get; }
        internal uint OriginalTtl { get; }
        internal uint Expiration { get; }
        internal uint Inception { get; }
        internal ushort KeyTag { get; }
        internal string SignerName { get; }
        internal byte[] Signature { get; }
    }

    internal static class DnsSecWire {
        internal static DnsWireResourceRecord[] Records(DnsResponse response) =>
            (response.WireAnswers ?? Array.Empty<DnsWireResourceRecord>())
                .Concat(response.WireAuthorities ?? Array.Empty<DnsWireResourceRecord>())
                .Concat(response.WireAdditional ?? Array.Empty<DnsWireResourceRecord>())
                .Where(record => record.Type != DnsRecordType.OPT)
                .ToArray();

        internal static bool TryReadKey(byte[] message, DnsWireResourceRecord record, out DnsSecKey key) {
            key = default;
            if (record.Type != DnsRecordType.DNSKEY || record.RdataLength < 4) return false;
            var reader = Reader(message, record);
            ushort flags = reader.ReadUInt16();
            byte protocol = reader.ReadByte();
            byte algorithm = reader.ReadByte();
            byte[] publicKey = reader.ReadBytes(reader.End - reader.Position);
            if (protocol != 3 || publicKey.Length == 0) return false;
            key = new DnsSecKey(record.Name, flags, protocol, algorithm, publicKey);
            return true;
        }

        internal static bool TryReadSignature(byte[] message, DnsWireResourceRecord record, out DnsSecSignature signature) {
            signature = default;
            if (record.Type != DnsRecordType.RRSIG || record.RdataLength < 19) return false;
            try {
                var reader = Reader(message, record);
                var typeCovered = (DnsRecordType)reader.ReadUInt16();
                byte algorithm = reader.ReadByte();
                byte labels = reader.ReadByte();
                uint originalTtl = reader.ReadUInt32();
                uint expiration = reader.ReadUInt32();
                uint inception = reader.ReadUInt32();
                ushort keyTag = reader.ReadUInt16();
                string signerName = reader.ReadName();
                byte[] value = reader.ReadBytes(reader.End - reader.Position);
                if (value.Length == 0) return false;
                signature = new DnsSecSignature(record.Name, record.Class, typeCovered, algorithm, labels,
                    originalTtl, expiration, inception, keyTag, signerName, value);
                return true;
            } catch (DnsClientException) {
                return false;
            }
        }

        internal static byte[] BuildSignedData(byte[] message, DnsSecSignature signature,
            IReadOnlyCollection<DnsWireResourceRecord> records) {
            using var output = new MemoryStream();
            WriteUInt16(output, (ushort)signature.TypeCovered);
            output.WriteByte(signature.Algorithm);
            output.WriteByte(signature.Labels);
            WriteUInt32(output, signature.OriginalTtl);
            WriteUInt32(output, signature.Expiration);
            WriteUInt32(output, signature.Inception);
            WriteUInt16(output, signature.KeyTag);
            WriteBytes(output, DnsWireNameCodec.ToCanonicalWire(signature.SignerName));

            string canonicalOwner = SignedOwner(signature.Owner, signature.Labels);
            var canonicalRecords = new List<byte[]>();
            foreach (DnsWireResourceRecord record in records) {
                if (record.Type != signature.TypeCovered || record.Class != signature.RecordClass ||
                    !string.Equals(DnsWireNameCodec.Canonical(record.Name), signature.Owner, StringComparison.Ordinal)) continue;
                byte[] rdata = CanonicalRdata(message, record);
                using var rr = new MemoryStream();
                WriteBytes(rr, DnsWireNameCodec.ToCanonicalWire(canonicalOwner));
                WriteUInt16(rr, (ushort)record.Type);
                WriteUInt16(rr, record.Class);
                WriteUInt32(rr, signature.OriginalTtl);
                WriteUInt16(rr, checked((ushort)rdata.Length));
                WriteBytes(rr, rdata);
                canonicalRecords.Add(rr.ToArray());
            }

            if (canonicalRecords.Count == 0) throw new DnsClientException("The RRSIG does not cover a matching RRset.");
            canonicalRecords.Sort(ByteArrayComparer.Instance);
            foreach (byte[] rr in canonicalRecords) WriteBytes(output, rr);
            return output.ToArray();
        }

        internal static byte[] CanonicalRdata(byte[] message, DnsWireResourceRecord record) {
            var reader = Reader(message, record);
            using var output = new MemoryStream();
            switch (record.Type) {
                case DnsRecordType.NS:
                case DnsRecordType.CNAME:
                case DnsRecordType.DNAME:
                case DnsRecordType.PTR:
                case DnsRecordType.MB:
                case DnsRecordType.MD:
                case DnsRecordType.MF:
                case DnsRecordType.MG:
                case DnsRecordType.MR:
                    WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.MX:
                case DnsRecordType.AFSDB:
                case DnsRecordType.RT:
                case DnsRecordType.KX:
                    WriteUInt16(output, reader.ReadUInt16());
                    WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.PX:
                    WriteUInt16(output, reader.ReadUInt16());
                    WriteCanonicalName(output, reader.ReadName());
                    WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.SOA:
                    WriteCanonicalName(output, reader.ReadName());
                    WriteCanonicalName(output, reader.ReadName());
                    WriteBytes(output, reader.ReadBytes(reader.End - reader.Position));
                    break;
                case DnsRecordType.MINFO:
                case DnsRecordType.RP:
                    WriteCanonicalName(output, reader.ReadName());
                    WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.SRV:
                    WriteBytes(output, reader.ReadBytes(6));
                    WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.A6:
                    byte prefixLength = reader.ReadByte();
                    if (prefixLength > 128) throw new DnsClientException("A6 prefix length exceeds 128 bits.");
                    output.WriteByte(prefixLength);
                    int suffixLength = (128 - prefixLength + 7) / 8;
                    WriteBytes(output, reader.ReadBytes(suffixLength));
                    if (prefixLength != 0) WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.NAPTR:
                    WriteBytes(output, reader.ReadBytes(4));
                    CopyCharacterString(reader, output);
                    CopyCharacterString(reader, output);
                    CopyCharacterString(reader, output);
                    WriteCanonicalName(output, reader.ReadName());
                    break;
                case DnsRecordType.NSEC:
                case DnsRecordType.NXT:
                    WriteCanonicalName(output, reader.ReadName());
                    WriteBytes(output, reader.ReadBytes(reader.End - reader.Position));
                    break;
                case DnsRecordType.SIG:
                case DnsRecordType.RRSIG:
                    WriteBytes(output, reader.ReadBytes(18));
                    WriteCanonicalName(output, reader.ReadName());
                    WriteBytes(output, reader.ReadBytes(reader.End - reader.Position));
                    break;
                case DnsRecordType.SVCB:
                case DnsRecordType.HTTPS:
                    WriteUInt16(output, reader.ReadUInt16());
                    WriteCanonicalName(output, reader.ReadName());
                    WriteBytes(output, reader.ReadBytes(reader.End - reader.Position));
                    break;
                default:
                    WriteBytes(output, reader.ReadBytes(reader.End - reader.Position));
                    break;
            }
            if (!reader.IsAtEnd) throw new DnsClientException($"{record.Type} RDATA was not consumed while canonicalizing DNSSEC data.");
            return output.ToArray();
        }

        internal static bool SignatureTimeIsValid(DnsSecSignature signature, DateTimeOffset now) {
            uint current = unchecked((uint)now.ToUnixTimeSeconds());
            return SerialLessOrEqual(signature.Inception, current) && SerialLessOrEqual(current, signature.Expiration);
        }

        private static bool SerialLessOrEqual(uint left, uint right) => unchecked((int)(left - right)) <= 0;

        private static string SignedOwner(string owner, byte labels) {
            if (owner == ".") return owner;
            string[] parts = owner.TrimEnd('.').Split('.');
            if (labels > parts.Length) throw new DnsClientException("RRSIG labels exceeds the owner-name label count.");
            if (labels == parts.Length) return owner;
            return "*." + string.Join(".", parts.Skip(parts.Length - labels)) + ".";
        }

        private static DnsWireReader Reader(byte[] message, DnsWireResourceRecord record) =>
            new(message, record.RdataOffset, checked(record.RdataOffset + record.RdataLength));

        private static void CopyCharacterString(DnsWireReader reader, Stream output) {
            byte length = reader.ReadByte();
            output.WriteByte(length);
            WriteBytes(output, reader.ReadBytes(length));
        }

        private static void WriteCanonicalName(Stream output, string name) =>
            WriteBytes(output, DnsWireNameCodec.ToCanonicalWire(name));

        private static void WriteUInt16(Stream output, ushort value) {
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void WriteUInt32(Stream output, uint value) {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void WriteBytes(Stream output, byte[] value) => output.Write(value, 0, value.Length);

        private sealed class ByteArrayComparer : IComparer<byte[]> {
            internal static readonly ByteArrayComparer Instance = new();
            public int Compare(byte[]? x, byte[]? y) {
                if (ReferenceEquals(x, y)) return 0;
                if (x is null) return -1;
                if (y is null) return 1;
                int count = Math.Min(x.Length, y.Length);
                for (int i = 0; i < count; i++) {
                    int result = x[i].CompareTo(y[i]);
                    if (result != 0) return result;
                }
                return x.Length.CompareTo(y.Length);
            }
        }
    }
}
