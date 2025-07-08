using System;
using System.IO;
using System.Net;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// Helper methods for constructing DNS UPDATE messages.
    /// </summary>
    internal static class DnsUpdateMessage {
        private static void WriteUInt16(Stream stream, ushort value) {
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)value));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteUInt32(Stream stream, uint value) {
            var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)value));
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteName(Stream stream, string name) {
            foreach (var part in name.TrimEnd('.').Split('.')) {
                var bytes = Encoding.ASCII.GetBytes(part);
                stream.WriteByte((byte)bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
            stream.WriteByte(0);
        }

        private static byte[] BuildRdata(DnsRecordType type, string data) {
            return type switch {
                DnsRecordType.A => IPAddress.Parse(data).GetAddressBytes(),
                DnsRecordType.AAAA => IPAddress.Parse(data).GetAddressBytes(),
                DnsRecordType.CNAME or DnsRecordType.NS => BuildNameRdata(data),
                DnsRecordType.TXT => BuildTxtRdata(data),
                _ => Encoding.ASCII.GetBytes(data)
            };
        }

        private static byte[] BuildNameRdata(string name) {
            using var ms = new MemoryStream();
            WriteName(ms, name);
            return ms.ToArray();
        }

        private static byte[] BuildTxtRdata(string text) {
            using var ms = new MemoryStream();
            if (text.Length > 255) {
                var parts = text.Split(' ');
                foreach (var part in parts) {
                    var bytes = Encoding.ASCII.GetBytes(part);
                    ms.WriteByte((byte)bytes.Length);
                    ms.Write(bytes, 0, bytes.Length);
                }
            } else {
                var bytes = Encoding.ASCII.GetBytes(text);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Creates a wire formatted message for adding a DNS record.
        /// </summary>
        /// <param name="zone">Zone to update.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Record type.</param>
        /// <param name="data">Record data.</param>
        /// <param name="ttl">Time to live of the record.</param>
        /// <returns>Serialized DNS UPDATE packet.</returns>
        internal static byte[] CreateAddMessage(string zone, string name, DnsRecordType type, string data, int ttl) {
            using var ms = new MemoryStream();
            var rand = new Random();
            WriteUInt16(ms, (ushort)rand.Next(ushort.MinValue, ushort.MaxValue));
            WriteUInt16(ms, 0x2800); // opcode UPDATE
            WriteUInt16(ms, 1); // zone count
            WriteUInt16(ms, 0); // prereq count
            WriteUInt16(ms, 1); // update count
            WriteUInt16(ms, 0); // additional
            WriteName(ms, zone);
            WriteUInt16(ms, (ushort)DnsRecordType.SOA);
            WriteUInt16(ms, 1); // class IN
            WriteName(ms, name);
            WriteUInt16(ms, (ushort)type);
            WriteUInt16(ms, 1);
            WriteUInt32(ms, (uint)ttl);
            var rdata = BuildRdata(type, data);
            WriteUInt16(ms, (ushort)rdata.Length);
            ms.Write(rdata, 0, rdata.Length);
            return ms.ToArray();
        }

        /// <summary>
        /// Creates a wire formatted message for deleting a DNS record.
        /// </summary>
        /// <param name="zone">Zone containing the record.</param>
        /// <param name="name">Record name.</param>
        /// <param name="type">Record type.</param>
        /// <returns>Serialized DNS UPDATE packet.</returns>
        internal static byte[] CreateDeleteMessage(string zone, string name, DnsRecordType type) {
            using var ms = new MemoryStream();
            var rand = new Random();
            WriteUInt16(ms, (ushort)rand.Next(ushort.MinValue, ushort.MaxValue));
            WriteUInt16(ms, 0x2800); // opcode UPDATE
            WriteUInt16(ms, 1); // zone count
            WriteUInt16(ms, 0); // prereq
            WriteUInt16(ms, 1); // update count
            WriteUInt16(ms, 0); // additional
            WriteName(ms, zone);
            WriteUInt16(ms, (ushort)DnsRecordType.SOA);
            WriteUInt16(ms, 1);
            WriteName(ms, name);
            WriteUInt16(ms, (ushort)type);
            WriteUInt16(ms, 255); // class ANY for delete
            WriteUInt32(ms, 0);
            WriteUInt16(ms, 0);
            return ms.ToArray();
        }
    }
}
