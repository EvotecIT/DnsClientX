using System;

namespace DnsClientX {
    /// <summary>
    /// Represents a parsed DNS message header (wire format).
    /// </summary>
    public readonly record struct DnsWireHeaderInfo(
        bool IsTruncated,
        bool IsRecursionAvailable,
        bool IsRecursionDesired,
        DnsResponseCode ResponseCode,
        ushort QuestionCount,
        ushort AnswerCount,
        ushort AuthorityCount,
        ushort AdditionalCount);

    /// <summary>
    /// Represents basic EDNS (OPT) information extracted from a DNS message (wire format).
    /// </summary>
    public readonly record struct DnsWireEdnsInfo(bool Supported, int UdpPayloadSize);

    /// <summary>
    /// Provides lightweight, safe parsing helpers for DNS wire-format messages.
    /// </summary>
    public static class DnsWireMessageParser {
        private const int DnsHeaderLength = 12;
        private const int MaxNameSegmentsToSkip = 50;

        /// <summary>
        /// Attempts to parse the DNS message header (flags and section counts).
        /// </summary>
        /// <param name="data">Raw DNS message bytes.</param>
        /// <param name="header">Parsed header fields.</param>
        /// <returns><c>true</c> when the header was parsed; otherwise <c>false</c>.</returns>
        public static bool TryParseHeader(byte[]? data, out DnsWireHeaderInfo header) {
            header = default;
            if (data == null || data.Length < DnsHeaderLength) {
                return false;
            }

            ushort flags = (ushort)((data[2] << 8) | data[3]);
            ushort qd = (ushort)((data[4] << 8) | data[5]);
            ushort an = (ushort)((data[6] << 8) | data[7]);
            ushort ns = (ushort)((data[8] << 8) | data[9]);
            ushort ar = (ushort)((data[10] << 8) | data[11]);

            header = new DnsWireHeaderInfo(
                IsTruncated: (flags & 0x0200) != 0,
                IsRecursionAvailable: (flags & 0x0080) != 0,
                IsRecursionDesired: (flags & 0x0100) != 0,
                ResponseCode: (DnsResponseCode)(flags & 0x000F),
                QuestionCount: qd,
                AnswerCount: an,
                AuthorityCount: ns,
                AdditionalCount: ar);

            return true;
        }

        /// <summary>
        /// Attempts to locate an EDNS OPT record and extract the advertised UDP payload size.
        /// </summary>
        /// <param name="data">Raw DNS message bytes.</param>
        /// <param name="edns">Parsed EDNS info.</param>
        /// <returns>
        /// <c>true</c> when parsing succeeded (even if EDNS is not present); otherwise <c>false</c> for malformed messages.
        /// </returns>
        public static bool TryParseEdns(byte[]? data, out DnsWireEdnsInfo edns) {
            edns = default;
            if (data == null || data.Length < DnsHeaderLength) {
                return false;
            }

            int offset = 4;
            if (!TryReadUInt16(data, ref offset, out var qd)) return false;
            if (!TryReadUInt16(data, ref offset, out var an)) return false;
            if (!TryReadUInt16(data, ref offset, out var ns)) return false;
            if (!TryReadUInt16(data, ref offset, out var ar)) return false;

            offset = DnsHeaderLength;
            for (int i = 0; i < qd; i++) {
                if (!TrySkipName(data, ref offset)) return false;
                if (offset + 4 > data.Length) return false;
                offset += 4; // QTYPE + QCLASS
            }

            int rrCount = an + ns + ar;
            for (int i = 0; i < rrCount; i++) {
                if (!TrySkipName(data, ref offset)) return false;
                if (!TryReadUInt16(data, ref offset, out var type)) return false;
                if (!TryReadUInt16(data, ref offset, out var rrClass)) return false;
                if (!TryReadUInt32(data, ref offset, out _)) return false; // TTL
                if (!TryReadUInt16(data, ref offset, out var rdlen)) return false;
                if (offset + rdlen > data.Length) return false;

                if (type == (ushort)DnsRecordType.OPT) {
                    edns = new DnsWireEdnsInfo(Supported: true, UdpPayloadSize: rrClass);
                    return true;
                }

                offset += rdlen;
            }

            edns = new DnsWireEdnsInfo(Supported: false, UdpPayloadSize: 0);
            return true;
        }

        private static bool TrySkipName(byte[] buffer, ref int offset) {
            int segments = 0;
            while (true) {
                if (buffer == null || offset < 0 || offset >= buffer.Length) {
                    return false;
                }

                var len = buffer[offset++];
                if (len == 0) {
                    return true;
                }

                // Compression pointer (RFC 1035 4.1.4): 2 bytes total.
                if ((len & 0xC0) == 0xC0) {
                    if (offset >= buffer.Length) {
                        return false;
                    }
                    offset++;
                    return true;
                }

                if (len > 63) {
                    return false;
                }

                if (offset + len > buffer.Length) {
                    return false;
                }

                offset += len;
                if (++segments > MaxNameSegmentsToSkip) {
                    return false;
                }
            }
        }

        private static bool TryReadUInt16(byte[] buffer, ref int offset, out ushort value) {
            value = 0;
            if (buffer == null || offset < 0 || offset + 2 > buffer.Length) {
                return false;
            }

            value = (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
            offset += 2;
            return true;
        }

        private static bool TryReadUInt32(byte[] buffer, ref int offset, out uint value) {
            value = 0;
            if (buffer == null || offset < 0 || offset + 4 > buffer.Length) {
                return false;
            }

            value = (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3]);
            offset += 4;
            return true;
        }
    }
}
