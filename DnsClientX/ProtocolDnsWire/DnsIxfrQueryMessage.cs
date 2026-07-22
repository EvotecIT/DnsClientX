using System;
using System.IO;
using System.Security.Cryptography;

namespace DnsClientX {
    internal readonly struct DnsIxfrQueryMessage {
        private DnsIxfrQueryMessage(ushort transactionId, byte[] wireData) {
            TransactionId = transactionId;
            WireData = wireData;
        }

        internal ushort TransactionId { get; }
        internal byte[] WireData { get; }

        internal static DnsIxfrQueryMessage Create(string zone, SoaRecord currentSoa, bool checkingDisabled) {
            if (currentSoa == null) throw new ArgumentNullException(nameof(currentSoa));
            ushort transactionId = CreateTransactionId();
            using var stream = new MemoryStream();
            WriteUInt16(stream, transactionId);
            WriteUInt16(stream, checkingDisabled ? (ushort)0x0010 : (ushort)0);
            WriteUInt16(stream, 1); // QDCOUNT
            WriteUInt16(stream, 0); // ANCOUNT
            WriteUInt16(stream, 1); // NSCOUNT: client's current SOA
            WriteUInt16(stream, 0); // ARCOUNT
            WriteName(stream, zone);
            WriteUInt16(stream, (ushort)DnsRecordType.IXFR);
            WriteUInt16(stream, 1);

            WriteName(stream, zone);
            WriteUInt16(stream, (ushort)DnsRecordType.SOA);
            WriteUInt16(stream, 1);
            WriteUInt32(stream, 0);
            using var rdata = new MemoryStream();
            WriteName(rdata, currentSoa.PrimaryNameServer);
            WriteName(rdata, currentSoa.ResponsiblePerson);
            WriteUInt32(rdata, currentSoa.Serial);
            WriteUInt32(rdata, currentSoa.Refresh);
            WriteUInt32(rdata, currentSoa.Retry);
            WriteUInt32(rdata, currentSoa.Expire);
            WriteUInt32(rdata, currentSoa.Minimum);
            byte[] rdataBytes = rdata.ToArray();
            WriteUInt16(stream, checked((ushort)rdataBytes.Length));
            stream.Write(rdataBytes, 0, rdataBytes.Length);
            return new DnsIxfrQueryMessage(transactionId, stream.ToArray());
        }

        private static ushort CreateTransactionId() {
            byte[] bytes = new byte[2];
            using RandomNumberGenerator generator = RandomNumberGenerator.Create();
            generator.GetBytes(bytes);
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static void WriteName(Stream stream, string name) {
            foreach (byte[] label in DnsWireNameCodec.EncodeLabels(DnsWireNameCodec.Normalize(name))) {
                stream.WriteByte((byte)label.Length);
                stream.Write(label, 0, label.Length);
            }
            stream.WriteByte(0);
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteUInt32(Stream stream, uint value) {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
