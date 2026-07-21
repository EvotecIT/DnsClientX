using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DnsClientX {
    /// <summary>
    /// Represents one DNS query message.
    /// </summary>
    public sealed class DnsMessage {
        private readonly byte[][] _labels;
        private readonly DnsRecordType _type;
        private readonly bool _recursionDesired;
        private readonly bool _requestDnsSec;
        private readonly bool _enableEdns;
        private readonly int _udpBufferSize;
        private readonly EdnsClientSubnetOption? _subnet;
        private readonly bool _checkingDisabled;
        private readonly EdnsOption[] _ednsOptions;
        private readonly ushort _queryClass;

        /// <summary>
        /// Initializes a DNS query with default recursion and EDNS settings.
        /// </summary>
        public DnsMessage(string name, DnsRecordType type, bool requestDnsSec)
            : this(name, type, new DnsMessageOptions(requestDnsSec, requestDnsSec)) {
        }

        /// <summary>
        /// Initializes a DNS query with advanced wire options.
        /// </summary>
        public DnsMessage(string name, DnsRecordType type, bool requestDnsSec, bool enableEdns,
            int udpBufferSize, string? subnet, bool checkingDisabled,
            System.Collections.Generic.IEnumerable<EdnsOption>? options = null)
            : this(name, type, new DnsMessageOptions(requestDnsSec, enableEdns, udpBufferSize,
                string.IsNullOrEmpty(subnet) ? null : new EdnsClientSubnetOption(subnet!),
                checkingDisabled, options)) {
        }

        /// <summary>
        /// Initializes a DNS query with structured options.
        /// </summary>
        public DnsMessage(string name, DnsRecordType type, DnsMessageOptions options) {
            Name = DnsWireNameCodec.Normalize(name);
            _labels = DnsWireNameCodec.EncodeLabels(Name);
            _type = type;
            _recursionDesired = options.RecursionDesired;
            _requestDnsSec = options.RequestDnsSec;
            _ednsOptions = options.Options?.ToArray() ?? Array.Empty<EdnsOption>();
            _enableEdns = options.EnableEdns || options.RequestDnsSec || options.Subnet != null || _ednsOptions.Length > 0;
            if (_enableEdns && (options.UdpBufferSize < 512 || options.UdpBufferSize > ushort.MaxValue)) {
                throw new ArgumentOutOfRangeException(nameof(options), "EDNS UDP buffer size must be between 512 and 65535 bytes.");
            }
            _udpBufferSize = options.UdpBufferSize;
            _subnet = options.Subnet;
            _checkingDisabled = options.CheckingDisabled;
            _queryClass = options.QueryClass;
            TransactionId = options.TransactionId ?? CreateTransactionId();
        }

        /// <summary>
        /// Gets the transaction identifier encoded in this message.
        /// </summary>
        public ushort TransactionId { get; }

        /// <summary>
        /// Gets the normalized absolute query name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the query record type.
        /// </summary>
        public DnsRecordType Type => _type;

        /// <summary>
        /// Gets the DNS question class. Internet (IN) is 1 and CHAOS is 3.
        /// </summary>
        public ushort QueryClass => _queryClass;

        /// <summary>
        /// Converts this message to unpadded base64url without changing its transaction ID.
        /// </summary>
        public string ToBase64Url() {
            string value = Convert.ToBase64String(SerializeDnsWireFormat());
            return value.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Serializes this message in DNS wire format.
        /// </summary>
        public byte[] SerializeDnsWireFormat() {
            using var stream = new MemoryStream();
            WriteUInt16(stream, TransactionId);

            ushort flags = 0;
            if (_recursionDesired) flags |= 0x0100;
            if (_checkingDisabled) flags |= 0x0010; // RFC 4035: CD is a DNS header flag.
            WriteUInt16(stream, flags);
            WriteUInt16(stream, 1); // QDCOUNT
            WriteUInt16(stream, 0); // ANCOUNT
            WriteUInt16(stream, 0); // NSCOUNT
            WriteUInt16(stream, _enableEdns ? (ushort)1 : (ushort)0); // ARCOUNT

            foreach (byte[] label in _labels) {
                stream.WriteByte((byte)label.Length);
                stream.Write(label, 0, label.Length);
            }
            stream.WriteByte(0);
            WriteUInt16(stream, (ushort)_type);
            WriteUInt16(stream, QueryClass);

            if (_enableEdns) {
                byte[] optionData = BuildOptions();
                stream.WriteByte(0); // OPT owner is the root name.
                WriteUInt16(stream, (ushort)DnsRecordType.OPT);
                WriteUInt16(stream, (ushort)_udpBufferSize);
                uint optTtl = _requestDnsSec ? 0x00008000u : 0u; // DO is the only flag set in OPT.Z.
                WriteUInt32(stream, optTtl);
                WriteUInt16(stream, checked((ushort)optionData.Length));
                stream.Write(optionData, 0, optionData.Length);
            }

            return stream.ToArray();
        }

        private byte[] BuildOptions() {
            using var stream = new MemoryStream();
            if (_subnet != null) {
                byte[] bytes = new EcsOption(_subnet.Value.Subnet).ToByteArray();
                stream.Write(bytes, 0, bytes.Length);
            }
            foreach (EdnsOption option in _ednsOptions) {
                byte[] bytes = option.ToByteArray();
                stream.Write(bytes, 0, bytes.Length);
            }
            return stream.ToArray();
        }

        private static ushort CreateTransactionId() {
            byte[] bytes = new byte[2];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(bytes);
            }
            return BinaryPrimitives.ReadUInt16BigEndian(bytes);
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            byte[] buffer = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static void WriteUInt32(Stream stream, uint value) {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
