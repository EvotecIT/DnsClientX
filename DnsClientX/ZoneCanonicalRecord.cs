using System;
using System.IO;

namespace DnsClientX {
    internal readonly struct ZoneCanonicalRecord {
        private ZoneCanonicalRecord(string name, DnsRecordType type, ushort recordClass, uint ttl,
            byte[] canonicalRdata, byte[] canonicalWire) {
            Name = name;
            Type = type;
            Class = recordClass;
            Ttl = ttl;
            CanonicalRdata = canonicalRdata;
            CanonicalWire = canonicalWire;
        }

        internal string Name { get; }
        internal DnsRecordType Type { get; }
        internal ushort Class { get; }
        internal uint Ttl { get; }
        internal byte[] CanonicalRdata { get; }
        internal byte[] CanonicalWire { get; }

        internal static ZoneCanonicalRecord Create(byte[] message, DnsWireResourceRecord record) {
            string canonicalName = DnsWireNameCodec.Canonical(record.Name);
            byte[] canonicalRdata = DnsSecWire.CanonicalRdata(message, record);
            using var output = new MemoryStream();
            Write(output, DnsWireNameCodec.ToCanonicalWire(canonicalName));
            WriteUInt16(output, (ushort)record.Type);
            WriteUInt16(output, record.Class);
            WriteUInt32(output, record.RawTtl);
            WriteUInt16(output, checked((ushort)canonicalRdata.Length));
            Write(output, canonicalRdata);
            return new ZoneCanonicalRecord(canonicalName, record.Type, record.Class, record.RawTtl,
                canonicalRdata, output.ToArray());
        }

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

        private static void Write(Stream output, byte[] value) => output.Write(value, 0, value.Length);
    }
}
