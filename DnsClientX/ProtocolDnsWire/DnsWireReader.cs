using System;
using System.Collections.Generic;
using System.Text;

namespace DnsClientX {
    internal sealed class DnsWireReader {
        private readonly byte[] _message;
        private readonly int _end;

        internal DnsWireReader(byte[] message, int offset = 0, int? end = null) {
            _message = message ?? throw new ArgumentNullException(nameof(message));
            Position = offset;
            _end = end ?? message.Length;
            if (offset < 0 || _end < offset || _end > message.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        }

        internal byte[] Message => _message;
        internal int Position { get; private set; }
        internal int End => _end;
        internal bool IsAtEnd => Position == _end;

        internal byte ReadByte() {
            Ensure(1);
            return _message[Position++];
        }

        internal ushort ReadUInt16() {
            Ensure(2);
            ushort value = (ushort)((_message[Position] << 8) | _message[Position + 1]);
            Position += 2;
            return value;
        }

        internal uint ReadUInt32() {
            Ensure(4);
            uint value = ((uint)_message[Position] << 24) | ((uint)_message[Position + 1] << 16) |
                         ((uint)_message[Position + 2] << 8) | _message[Position + 3];
            Position += 4;
            return value;
        }

        internal byte[] ReadBytes(int count) {
            Ensure(count);
            var value = new byte[count];
            Buffer.BlockCopy(_message, Position, value, 0, count);
            Position += count;
            return value;
        }

        internal void Skip(int count) {
            Ensure(count);
            Position += count;
        }

        internal string ReadName() {
            int directPosition = Position;
            int current = Position;
            bool jumped = false;
            int expandedLength = 1;
            int pointerCount = 0;
            var visited = new HashSet<int>();
            var labels = new List<string>();

            while (true) {
                if (current < 0 || current >= _message.Length) throw new DnsClientException("DNS name extends beyond the message boundary.");
                if (!jumped && current >= _end) throw new DnsClientException("DNS name extends beyond its RDATA boundary.");
                byte length = _message[current++];
                if (length == 0) {
                    if (!jumped) directPosition = current;
                    Position = directPosition;
                    return labels.Count == 0 ? "." : string.Join(".", labels) + ".";
                }

                if ((length & 0xC0) == 0xC0) {
                    if (current >= _message.Length || (!jumped && current >= _end)) throw new DnsClientException("DNS compression pointer is truncated.");
                    int pointer = ((length & 0x3F) << 8) | _message[current++];
                    int pointerSource = current - 2;
                    if (pointer >= pointerSource) throw new DnsClientException("DNS compression pointer must point to an earlier message offset.");
                    if (!visited.Add(pointer) || ++pointerCount > 128) throw new DnsClientException("DNS compression pointer loop detected.");
                    if (!jumped) directPosition = current;
                    current = pointer;
                    jumped = true;
                    continue;
                }

                if ((length & 0xC0) != 0 || length > 63) throw new DnsClientException("DNS label has an invalid length or reserved label type.");
                int limit = jumped ? _message.Length : _end;
                if (current + length > limit) throw new DnsClientException("DNS label extends beyond the message boundary.");
                expandedLength += length + 1;
                if (expandedLength > 255) throw new DnsClientException("Expanded DNS name exceeds 255 octets.");
                labels.Add(ToPresentationLabel(_message, current, length));
                current += length;
                if (!jumped) directPosition = current;
            }
        }

        private static string ToPresentationLabel(byte[] message, int offset, int length) {
            var builder = new StringBuilder(length);
            for (int i = 0; i < length; i++) {
                byte value = message[offset + i];
                if (value == (byte)'.' || value == (byte)'\\') {
                    builder.Append('\\').Append((char)value);
                } else if (value < 0x21 || value > 0x7E) {
                    builder.Append('\\').Append(value.ToString("D3", System.Globalization.CultureInfo.InvariantCulture));
                } else {
                    builder.Append((char)value);
                }
            }
            return builder.ToString();
        }

        private void Ensure(int count) {
            if (count < 0 || Position > _end - count) throw new DnsClientException("DNS message is truncated.");
        }
    }

    internal readonly record struct DnsWireResourceRecord(
        string Name,
        DnsRecordType Type,
        ushort Class,
        int Ttl,
        uint RawTtl,
        int RdataOffset,
        ushort RdataLength,
        string Data);
}
