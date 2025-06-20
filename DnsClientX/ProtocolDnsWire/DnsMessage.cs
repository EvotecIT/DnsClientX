using System;
using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// DnsMessage class
    /// </summary>
    public class DnsMessage {
        private readonly string _name;
        private readonly DnsRecordType _type;
        private readonly bool _requestDnsSec;
        private const ushort _ednsUdpPayloadSize = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="DnsMessage"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The type.</param>
        /// <param name="requestDnsSec">if set to <c>true</c> [request DNS sec].</param>
        public DnsMessage(string name, DnsRecordType type, bool requestDnsSec) {
            _name = name;
            _type = type;
            _requestDnsSec = requestDnsSec;
        }

        /// <summary>
        /// Converts DNS message to base64url format
        /// </summary>
        /// <returns></returns>
        public string ToBase64Url() {
            using var stream = new MemoryStream();

            // Temporary buffer
            Span<byte> buffer = stackalloc byte[2];

            // Write the ID
            Random random = new Random();
            ushort randomId = (ushort)random.Next(ushort.MinValue, ushort.MaxValue);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, randomId);
            //BinaryPrimitives.WriteUInt16BigEndian(buffer, 0xABCD);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            //BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            //stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the flags
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0x0100);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the flags
            //BinaryPrimitives.WriteUInt16BigEndian(buffer, 0x0000);
            //stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the question count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the answer count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the authority count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the additional count (for EDNS0 OPT record)
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the question name
            foreach (var label in _name.Split('.')) {
                var labelBytes = Encoding.ASCII.GetBytes(label);
                stream.WriteByte((byte)labelBytes.Length); // Write the length of the label
                stream.Write(labelBytes, 0, labelBytes.Length);
            }
            stream.WriteByte(0); // End of name

            // Write the question type
            BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)_type);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Write the question class
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 1);
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Append OPT record for EDNS0
            stream.WriteByte(0); // root name
            BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)DnsRecordType.OPT);
            stream.Write(buffer.ToArray(), 0, buffer.Length);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, _ednsUdpPayloadSize);
            stream.Write(buffer.ToArray(), 0, buffer.Length);
            stream.WriteByte(0); // extended rcode
            stream.WriteByte(0); // edns version
            BinaryPrimitives.WriteUInt16BigEndian(buffer, _requestDnsSec ? (ushort)0x8000 : (ushort)0);
            stream.Write(buffer.ToArray(), 0, buffer.Length);
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0); // rdlen
            stream.Write(buffer.ToArray(), 0, buffer.Length);

            // Convert to base64url format
            var dnsMessageBytes = stream.ToArray();
            string base64Url = Convert.ToBase64String(dnsMessageBytes).TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            return base64Url;
        }

        /// <summary>
        /// Serializes the DNS wire format
        /// </summary>
        /// <returns></returns>
        public byte[] SerializeDnsWireFormat() {
            using (var ms = new MemoryStream()) {
                // Transaction ID
                Random random = new Random();
                ushort randomId = (ushort)random.Next(ushort.MinValue, ushort.MaxValue);
                var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)randomId));
                ms.Write(bytes, 0, bytes.Length);

                // Flags
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0x0100)); // Standard query
                ms.Write(bytes, 0, bytes.Length);

                // Questions
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)1));
                ms.Write(bytes, 0, bytes.Length);

                // Answer RRs
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0));
                ms.Write(bytes, 0, bytes.Length);

                // Authority RRs
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0));
                ms.Write(bytes, 0, bytes.Length);

                // Additional RRs (for EDNS0 OPT record)
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)1));
                ms.Write(bytes, 0, bytes.Length);

                // Queries
                foreach (var part in _name.Split('.')) {
                    ms.WriteByte((byte)part.Length);
                    var partBytes = Encoding.ASCII.GetBytes(part);
                    ms.Write(partBytes, 0, partBytes.Length);
                }
                ms.WriteByte((byte)0); // End of name

                // Type
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)_type));
                ms.Write(bytes, 0, bytes.Length);

                // Class
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)1)); // IN
                ms.Write(bytes, 0, bytes.Length);

                // Append OPT record for EDNS0
                ms.WriteByte(0); // root name
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)DnsRecordType.OPT));
                ms.Write(bytes, 0, bytes.Length);
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)_ednsUdpPayloadSize));
                ms.Write(bytes, 0, bytes.Length);
                ms.WriteByte(0); // extended rcode
                ms.WriteByte(0); // edns version
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)(_requestDnsSec ? 0x8000 : 0)));
                ms.Write(bytes, 0, bytes.Length);
                bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)0)); // rdlen
                ms.Write(bytes, 0, bytes.Length);

                return ms.ToArray();
            }
        }
    }
}
