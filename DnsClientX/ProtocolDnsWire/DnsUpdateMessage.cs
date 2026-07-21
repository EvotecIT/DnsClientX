using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace DnsClientX {
    internal readonly struct DnsUpdateRequestMessage {
        internal DnsUpdateRequestMessage(ushort transactionId, string zone, byte[] wireData, byte[] tsigMac) {
            TransactionId = transactionId;
            Zone = zone;
            WireData = wireData;
            TsigMac = tsigMac;
        }
        internal ushort TransactionId { get; }
        internal string Zone { get; }
        internal byte[] WireData { get; }
        internal byte[] TsigMac { get; }
    }

    /// <summary>
    /// Constructs RFC 2136 DNS UPDATE messages with optional RFC 8945 TSIG authentication.
    /// </summary>
    internal static class DnsUpdateMessage {
        private const ushort ClassIn = 1;
        private const ushort ClassNone = 254;
        private const ushort ClassAny = 255;

        internal static DnsUpdateRequestMessage CreateAddMessage(string zone, string name, DnsRecordType type,
            string data, int ttl, TsigKey? key = null) =>
            Create(zone, name, type, ClassIn, checked((uint)ttl), BuildRdata(type, data), key);

        internal static DnsUpdateRequestMessage CreateDeleteRrsetMessage(string zone, string name,
            DnsRecordType type, TsigKey? key = null) =>
            Create(zone, name, type, ClassAny, 0, Array.Empty<byte>(), key);

        internal static DnsUpdateRequestMessage CreateDeleteValueMessage(string zone, string name,
            DnsRecordType type, string data, TsigKey? key = null) =>
            Create(zone, name, type, ClassNone, 0, BuildRdata(type, data), key);

        private static DnsUpdateRequestMessage Create(string zone, string name, DnsRecordType type,
            ushort recordClass, uint ttl, byte[] rdata, TsigKey? key) {
            zone = DnsWireNameCodec.Normalize(zone);
            name = DnsWireNameCodec.Normalize(name);
            if (zone != "." && !string.Equals(name, zone, StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith("." + zone, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException($"Record name {name} is outside update zone {zone}.", nameof(name));
            }
            ushort transactionId = CreateTransactionId();
            using var stream = new MemoryStream();
            WriteUInt16(stream, transactionId);
            WriteUInt16(stream, 0x2800); // OPCODE=UPDATE
            WriteUInt16(stream, 1); // ZOCOUNT
            WriteUInt16(stream, 0); // PRCOUNT
            WriteUInt16(stream, 1); // UPCOUNT
            WriteUInt16(stream, 0); // ADCOUNT before optional TSIG
            WriteName(stream, zone);
            WriteUInt16(stream, (ushort)DnsRecordType.SOA);
            WriteUInt16(stream, ClassIn);
            WriteName(stream, name);
            WriteUInt16(stream, (ushort)type);
            WriteUInt16(stream, recordClass);
            WriteUInt32(stream, ttl);
            WriteUInt16(stream, checked((ushort)rdata.Length));
            stream.Write(rdata, 0, rdata.Length);
            byte[] unsigned = stream.ToArray();
            if (key == null) return new DnsUpdateRequestMessage(transactionId, zone, unsigned, Array.Empty<byte>());
            DnsTsigSignedMessage signed = DnsTsig.Sign(unsigned, transactionId, key);
            return new DnsUpdateRequestMessage(transactionId, zone, signed.WireData, signed.Mac);
        }

        private static byte[] BuildRdata(DnsRecordType type, string data) {
            if (data == null) throw new ArgumentNullException(nameof(data));
            switch (type) {
                case DnsRecordType.A:
                    return RequireAddress(data, AddressFamily.InterNetwork, type);
                case DnsRecordType.AAAA:
                    return RequireAddress(data, AddressFamily.InterNetworkV6, type);
                case DnsRecordType.CNAME:
                case DnsRecordType.NS:
                case DnsRecordType.PTR:
                case DnsRecordType.DNAME:
                    return DnsWireNameCodec.ToCanonicalWire(data);
                case DnsRecordType.TXT:
                case DnsRecordType.SPF:
                    return BuildTxtRdata(Unquote(data));
                case DnsRecordType.MX:
                    return BuildPreferenceName(data, type);
                case DnsRecordType.SRV:
                    return BuildSrv(data);
                case DnsRecordType.CAA:
                    return BuildCaa(data);
                default:
                    throw new NotSupportedException($"RFC 2136 RDATA serialization for {type} is not implemented. Use a supported typed record instead of sending ambiguous ASCII bytes.");
            }
        }

        private static byte[] RequireAddress(string data, AddressFamily family, DnsRecordType type) {
            if (!IPAddress.TryParse(data, out IPAddress? address) || address.AddressFamily != family) {
                throw new ArgumentException($"{data} is not a valid {type} address.", nameof(data));
            }
            return address.GetAddressBytes();
        }

        private static byte[] BuildPreferenceName(string data, DnsRecordType type) {
            string[] parts = Split(data, 2, type);
            if (!ushort.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out ushort preference)) {
                throw new ArgumentException($"{type} preference must be an unsigned 16-bit integer.", nameof(data));
            }
            using var stream = new MemoryStream();
            WriteUInt16(stream, preference);
            WriteName(stream, parts[1]);
            return stream.ToArray();
        }

        private static byte[] BuildSrv(string data) {
            string[] parts = Split(data, 4, DnsRecordType.SRV);
            if (!ushort.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out ushort priority) ||
                !ushort.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out ushort weight) ||
                !ushort.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out ushort port)) {
                throw new ArgumentException("SRV priority, weight, and port must be unsigned 16-bit integers.", nameof(data));
            }
            using var stream = new MemoryStream();
            WriteUInt16(stream, priority);
            WriteUInt16(stream, weight);
            WriteUInt16(stream, port);
            WriteName(stream, parts[3]);
            return stream.ToArray();
        }

        private static byte[] BuildCaa(string data) {
            string[] parts = Split(data, 3, DnsRecordType.CAA);
            if (!byte.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out byte flags)) {
                throw new ArgumentException("CAA flags must be an unsigned 8-bit integer.", nameof(data));
            }
            string tagText = parts[1].ToLowerInvariant();
            if (tagText.Length < 1 || tagText.Length > 15 || tagText.Any(character =>
                !((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9')))) {
                throw new ArgumentException("CAA tag must contain 1-15 ASCII letters or digits.", nameof(data));
            }
            byte[] tag = Encoding.ASCII.GetBytes(tagText);
            byte[] value = Encoding.UTF8.GetBytes(Unquote(parts[2]));
            using var stream = new MemoryStream();
            stream.WriteByte(flags);
            stream.WriteByte((byte)tag.Length);
            stream.Write(tag, 0, tag.Length);
            stream.Write(value, 0, value.Length);
            return stream.ToArray();
        }

        private static byte[] BuildTxtRdata(string text) {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using var stream = new MemoryStream();
            if (bytes.Length == 0) stream.WriteByte(0);
            for (int offset = 0; offset < bytes.Length;) {
                int count = Math.Min(byte.MaxValue, bytes.Length - offset);
                stream.WriteByte((byte)count);
                stream.Write(bytes, offset, count);
                offset += count;
            }
            return stream.ToArray();
        }

        private static string[] Split(string data, int count, DnsRecordType type) {
            string[] parts = data.Trim().Split(new[] { ' ', '\t' }, count, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != count) throw new ArgumentException($"{type} data requires {count} fields.", nameof(data));
            return parts;
        }

        private static string Unquote(string value) {
            value = value.Trim();
            return value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"'
                ? value.Substring(1, value.Length - 2)
                : value;
        }

        private static ushort CreateTransactionId() {
            byte[] bytes = new byte[2];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }

        private static void WriteName(Stream stream, string name) {
            byte[] wire = DnsWireNameCodec.ToCanonicalWire(name);
            stream.Write(wire, 0, wire.Length);
        }
        private static void WriteUInt16(Stream stream, ushort value) { stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
        private static void WriteUInt32(Stream stream, uint value) { stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
    }
}
