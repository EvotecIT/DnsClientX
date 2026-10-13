using System;
using System.IO;
using System.Net;

namespace DnsClientX;
internal static partial class DnsWireResolveMulticast {
    /// <summary>Builds a bounded mDNS query with half-life-valid known answers and no cache-flush bits.</summary>
    internal static byte[] CreateBrowseQuery(string name, DnsRecordType type, DnsAnswer[] knownAnswers) {
        byte[] question = new DnsMessage(name, type, new DnsMessageOptions(RecursionDesired: false, TransactionId: 0, EnableEdns: false)).SerializeDnsWireFormat();
        using var message = new MemoryStream();
        message.Write(question, 0, question.Length);
        ushort count = 0;
        foreach (DnsAnswer record in knownAnswers) {
            byte[] owner = DnsWireNameCodec.ToCanonicalWire(record.Name);
            byte[]? data = BrowseRdata(record);
            if (data == null || message.Length + owner.Length + 10 + data.Length > 1300) {
                continue;
            }

            message.Write(owner, 0, owner.Length);
            BrowseUInt16(message, (ushort)record.Type);
            BrowseUInt16(message, 1); // Known answers must never carry the cache-flush bit.
            uint ttl = checked((uint)Math.Max(0, record.TTL));
            message.WriteByte((byte)(ttl >> 24));
            message.WriteByte((byte)(ttl >> 16));
            message.WriteByte((byte)(ttl >> 8));
            message.WriteByte((byte)ttl);
            BrowseUInt16(message, checked((ushort)data.Length));
            message.Write(data, 0, data.Length);
            count++;
        }

        byte[] bytes = message.ToArray();
        bytes[6] = (byte)(count >> 8);
        bytes[7] = (byte)count;
        return bytes;
    }

    private static byte[]? BrowseRdata(DnsAnswer record) {
        if (record.Type == DnsRecordType.PTR) {
            return DnsWireNameCodec.ToCanonicalWire(record.Data);
        }

        if (record.Type is DnsRecordType.A or DnsRecordType.AAAA) {
            return IPAddress.TryParse(record.Data, out IPAddress? address) ? address.GetAddressBytes() : null;
        }

        // TXT contains no compressed names; retaining the parser's exact octets avoids text re-encoding.
        if (record.Type == DnsRecordType.TXT) {
            return record.RawRdata;
        }

        if (record.TypedRecord is SrvRecord srv) {
            using var data = new MemoryStream();
            BrowseUInt16(data, srv.Priority);
            BrowseUInt16(data, srv.Weight);
            BrowseUInt16(data, srv.Port);
            byte[] target = DnsWireNameCodec.ToCanonicalWire(srv.Target);
            data.Write(target, 0, target.Length);
            return data.ToArray();
        }

        return null;
    }

    private static void BrowseUInt16(Stream stream, ushort value) {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
