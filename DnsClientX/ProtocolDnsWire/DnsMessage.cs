using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace DnsClientX {
    /// <summary>
    /// DnsMessage class
    /// </summary>
    public class DnsMessage {
        private readonly string _name;
        private readonly DnsRecordType _type;
        private readonly bool _requestDnsSec;

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

            // Write the additional count
            BinaryPrimitives.WriteUInt16BigEndian(buffer, 0);
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

            // Convert to base64url format
            var dnsMessageBytes = stream.ToArray();
            string base64Url = Convert.ToBase64String(dnsMessageBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            return base64Url;
        }

        /// <summary>
        /// Serializes the DNS wire format for POST query
        /// TODO: Doesn't work properly
        /// </summary>
        /// <returns></returns>
        public byte[] SerializeDnsWireFormat() {
            using (var ms = new MemoryStream()) {
                using (var writer = new BinaryWriter(ms)) {
                    // Transaction ID
                    Random random = new Random();
                    ushort randomId = (ushort)random.Next(ushort.MinValue, ushort.MaxValue);
                    writer.Write((ushort)randomId);
                    //writer.Write((ushort)1);

                    // Flags
                    writer.Write((ushort)0x0100); // Standard query
                    //writer.Write((ushort)0x0000);

                    // Questions
                    writer.Write((ushort)1);

                    // Answer RRs
                    writer.Write((ushort)0);

                    // Authority RRs
                    writer.Write((ushort)0);

                    // Additional RRs
                    writer.Write((ushort)0);

                    // Queries
                    foreach (var part in _name.Split('.')) {
                        writer.Write((byte)part.Length);
                        writer.Write(Encoding.ASCII.GetBytes(part));
                    }

                    writer.Write((byte)0); // End of name

                    // Type
                    writer.Write((ushort)_type);

                    // Class
                    writer.Write((ushort)1); // IN
                }

                return ms.ToArray();
            }
        }
    }
}
