using System;
using System.Collections.Generic;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsWireMessageParser"/> helpers.
    /// </summary>
    public class DnsWireMessageParserTests {
        private const int DnsHeaderLength = 12;
        private const ushort TcFlag = 0x0200;
        private const ushort RdFlag = 0x0100;
        private const ushort RaFlag = 0x0080;

        /// <summary>
        /// Ensures header parsing extracts flags and section counts.
        /// </summary>
        [Fact]
        public void TryParseHeader_ShouldParseFlagsAndCounts() {
            ushort flags = (ushort)(TcFlag | RdFlag | RaFlag | (ushort)DnsResponseCode.NXDomain);
            byte[] data = CreateHeader(flags, qd: 1, an: 2, ns: 3, ar: 4);

            bool ok = DnsWireMessageParser.TryParseHeader(data, out var header);

            Assert.True(ok);
            Assert.True(header.IsTruncated);
            Assert.True(header.IsRecursionAvailable);
            Assert.True(header.IsRecursionDesired);
            Assert.Equal(DnsResponseCode.NXDomain, header.ResponseCode);
            Assert.Equal((ushort)1, header.QuestionCount);
            Assert.Equal((ushort)2, header.AnswerCount);
            Assert.Equal((ushort)3, header.AuthorityCount);
            Assert.Equal((ushort)4, header.AdditionalCount);
        }

        /// <summary>
        /// Ensures header parsing fails for truncated data.
        /// </summary>
        [Fact]
        public void TryParseHeader_ShouldReturnFalse_WhenDataTooShort() {
            byte[] data = new byte[DnsHeaderLength - 1];

            bool ok = DnsWireMessageParser.TryParseHeader(data, out var header);

            Assert.False(ok);
            Assert.Equal(default, header);
        }

        /// <summary>
        /// Ensures EDNS parsing finds the OPT record and returns the UDP payload size.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnUdpPayloadSize_WhenOptPresent() {
            const ushort udpPayloadSize = 1232;
            var opts = new DnsMessageOptions(EnableEdns: true, UdpBufferSize: udpPayloadSize);
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);

            byte[] data = message.SerializeDnsWireFormat();

            bool ok = DnsWireMessageParser.TryParseEdns(data, out var edns);

            Assert.True(ok);
            Assert.True(edns.Supported);
            Assert.Equal(udpPayloadSize, edns.UdpPayloadSize);
        }

        /// <summary>
        /// Ensures EDNS parsing returns a non-supported result when no OPT record is present.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnUnsupported_WhenNoOptPresent() {
            var opts = new DnsMessageOptions(EnableEdns: false);
            var message = new DnsMessage("example.com", DnsRecordType.A, opts);

            byte[] data = message.SerializeDnsWireFormat();

            bool ok = DnsWireMessageParser.TryParseEdns(data, out var edns);

            Assert.True(ok);
            Assert.False(edns.Supported);
            Assert.Equal(0, edns.UdpPayloadSize);
        }

        /// <summary>
        /// Ensures EDNS parsing fails when the DNS message header is truncated.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnFalse_WhenDataTooShort() {
            byte[] data = new byte[DnsHeaderLength - 1];

            bool ok = DnsWireMessageParser.TryParseEdns(data, out _);

            Assert.False(ok);
        }

        /// <summary>
        /// Ensures EDNS parsing fails when questions are declared but not present.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnFalse_WhenQuestionIsTruncated() {
            byte[] data = CreateHeader(flags: 0, qd: 1, an: 0, ns: 0, ar: 0);

            bool ok = DnsWireMessageParser.TryParseEdns(data, out _);

            Assert.False(ok);
        }

        /// <summary>
        /// Ensures EDNS parsing fails for invalid label lengths (&gt; 63).
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnFalse_WhenQuestionLabelTooLong() {
            byte[] header = CreateHeader(flags: 0, qd: 1, an: 0, ns: 0, ar: 0);
            byte[] data = new byte[header.Length + 1];
            Array.Copy(header, 0, data, 0, header.Length);
            data[DnsHeaderLength] = 64;

            bool ok = DnsWireMessageParser.TryParseEdns(data, out _);

            Assert.False(ok);
        }

        /// <summary>
        /// Ensures EDNS parsing can handle compressed names in the question section.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldHandleCompressedQuestionName() {
            const ushort udpPayloadSize = 1232;
            var bytes = new List<byte>();

            byte[] header = CreateHeader(flags: 0, qd: 2, an: 0, ns: 0, ar: 1);
            bytes.AddRange(header);

            int firstQuestionNameOffset = bytes.Count;
            AppendQuestion(bytes, "example.com", DnsRecordType.A, qclass: 1);

            AppendCompressedQuestion(bytes, pointerOffset: (ushort)firstQuestionNameOffset, DnsRecordType.A, qclass: 1);

            AppendOptRecord(bytes, udpPayloadSize);

            bool ok = DnsWireMessageParser.TryParseEdns(bytes.ToArray(), out var edns);

            Assert.True(ok);
            Assert.True(edns.Supported);
            Assert.Equal(udpPayloadSize, edns.UdpPayloadSize);
        }

        /// <summary>
        /// Ensures EDNS parsing fails when a compression pointer is truncated.
        /// </summary>
        [Fact]
        public void TryParseEdns_ShouldReturnFalse_WhenCompressionPointerTruncated() {
            byte[] header = CreateHeader(flags: 0, qd: 1, an: 0, ns: 0, ar: 0);
            byte[] data = new byte[header.Length + 1];
            Array.Copy(header, 0, data, 0, header.Length);
            data[DnsHeaderLength] = 0xC0;

            bool ok = DnsWireMessageParser.TryParseEdns(data, out _);

            Assert.False(ok);
        }

        private static byte[] CreateHeader(ushort flags, ushort qd, ushort an, ushort ns, ushort ar) {
            var data = new byte[DnsHeaderLength];
            WriteUInt16At(data, 0, 1); // ID
            WriteUInt16At(data, 2, flags);
            WriteUInt16At(data, 4, qd);
            WriteUInt16At(data, 6, an);
            WriteUInt16At(data, 8, ns);
            WriteUInt16At(data, 10, ar);
            return data;
        }

        private static void WriteUInt16At(byte[] buffer, int offset, ushort value) {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }

        private static void AppendQuestion(List<byte> message, string name, DnsRecordType qtype, ushort qclass) {
            if (message == null) {
                throw new ArgumentNullException(nameof(message));
            }
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Name must not be empty.", nameof(name));
            }

            foreach (var label in name.TrimEnd('.').Split('.')) {
                message.Add((byte)label.Length);
                foreach (char c in label) {
                    message.Add((byte)c);
                }
            }
            message.Add(0x00);

            message.Add((byte)(((ushort)qtype >> 8) & 0xFF));
            message.Add((byte)((ushort)qtype & 0xFF));
            message.Add((byte)(qclass >> 8));
            message.Add((byte)(qclass & 0xFF));
        }

        private static void AppendCompressedQuestion(List<byte> message, ushort pointerOffset, DnsRecordType qtype, ushort qclass) {
            // Compression pointer: 11xx xxxx xxxx xxxx (14-bit offset).
            byte first = (byte)(0xC0 | ((pointerOffset >> 8) & 0x3F));
            byte second = (byte)(pointerOffset & 0xFF);

            message.Add(first);
            message.Add(second);

            message.Add((byte)(((ushort)qtype >> 8) & 0xFF));
            message.Add((byte)((ushort)qtype & 0xFF));
            message.Add((byte)(qclass >> 8));
            message.Add((byte)(qclass & 0xFF));
        }

        private static void AppendOptRecord(List<byte> message, ushort udpPayloadSize) {
            message.Add(0x00); // root name
            message.Add(0x00);
            message.Add(0x29); // TYPE OPT
            message.Add((byte)(udpPayloadSize >> 8));
            message.Add((byte)(udpPayloadSize & 0xFF));
            message.Add(0x00);
            message.Add(0x00);
            message.Add(0x00);
            message.Add(0x00); // TTL
            message.Add(0x00);
            message.Add(0x00); // RDLEN
        }
    }
}
