using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DnsClientX {
    internal readonly struct DnsTsigSignedMessage {
        internal DnsTsigSignedMessage(byte[] wireData, byte[] mac) {
            WireData = wireData;
            Mac = mac;
        }
        internal byte[] WireData { get; }
        internal byte[] Mac { get; }
    }

    internal static class DnsTsig {
        private const ushort TsigType = 250;
        private const ushort AnyClass = 255;
        internal static Func<DateTimeOffset> UtcNow { get; set; } = () => DateTimeOffset.UtcNow;

        internal static DnsTsigSignedMessage Sign(byte[] unsignedMessage, ushort originalId, TsigKey key, ushort fudge = 300) {
            if (unsignedMessage == null) throw new ArgumentNullException(nameof(unsignedMessage));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (unsignedMessage.Length < 12) throw new DnsClientException("A DNS message must contain a complete header before TSIG can be applied.");
            if (ReadUInt16(unsignedMessage, 10) != 0) throw new DnsClientException("TSIG signing requires a message without existing additional records.");

            ulong time = checked((ulong)UtcNow().ToUnixTimeSeconds());
            string algorithmName = AlgorithmName(key.Algorithm);
            byte[] variables = BuildVariables(key.Name, algorithmName, time, fudge, 0, Array.Empty<byte>());
            byte[] mac = ComputeMac(key, Combine(unsignedMessage, variables));

            return Append(unsignedMessage, originalId, key, algorithmName, time, fudge, mac);
        }

        internal static DnsTsigSignedMessage SignResponse(byte[] unsignedResponse, ushort originalId, TsigKey key,
            byte[] requestMac, ushort fudge = 300) {
            if (unsignedResponse == null) throw new ArgumentNullException(nameof(unsignedResponse));
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (requestMac == null || requestMac.Length == 0) throw new ArgumentException("A request MAC is required for a TSIG response.", nameof(requestMac));
            if (unsignedResponse.Length < 12 || ReadUInt16(unsignedResponse, 10) != 0) throw new DnsClientException("TSIG signing requires a response without existing additional records.");
            ulong time = checked((ulong)UtcNow().ToUnixTimeSeconds());
            string algorithmName = AlgorithmName(key.Algorithm);
            byte[] variables = BuildVariables(key.Name, algorithmName, time, fudge, 0, Array.Empty<byte>());
            byte[] macMessage = (byte[])unsignedResponse.Clone();
            WriteUInt16(macMessage, 0, originalId);
            using var input = new MemoryStream();
            WriteUInt16(input, checked((ushort)requestMac.Length));
            input.Write(requestMac, 0, requestMac.Length);
            input.Write(macMessage, 0, macMessage.Length);
            input.Write(variables, 0, variables.Length);
            byte[] mac = ComputeMac(key, input.ToArray());
            return Append(unsignedResponse, originalId, key, algorithmName, time, fudge, mac);
        }

        private static DnsTsigSignedMessage Append(byte[] unsignedMessage, ushort originalId, TsigKey key,
            string algorithmName, ulong time, ushort fudge, byte[] mac) {

            using var output = new MemoryStream();
            byte[] headerAndBody = (byte[])unsignedMessage.Clone();
            WriteUInt16(headerAndBody, 10, 1);
            output.Write(headerAndBody, 0, headerAndBody.Length);
            WriteName(output, key.Name);
            WriteUInt16(output, TsigType);
            WriteUInt16(output, AnyClass);
            WriteUInt32(output, 0);
            using var rdata = new MemoryStream();
            WriteName(rdata, algorithmName);
            WriteUInt48(rdata, time);
            WriteUInt16(rdata, fudge);
            WriteUInt16(rdata, checked((ushort)mac.Length));
            rdata.Write(mac, 0, mac.Length);
            WriteUInt16(rdata, originalId);
            WriteUInt16(rdata, 0);
            WriteUInt16(rdata, 0);
            byte[] rdataBytes = rdata.ToArray();
            WriteUInt16(output, checked((ushort)rdataBytes.Length));
            output.Write(rdataBytes, 0, rdataBytes.Length);
            return new DnsTsigSignedMessage(output.ToArray(), mac);
        }

        internal static void VerifyResponse(byte[] response, ushort expectedId, TsigKey key, byte[] requestMac) {
            if (response == null || response.Length < 12) throw new DnsClientException("The TSIG response is truncated.");
            ushort additionalCount = ReadUInt16(response, 10);
            if (additionalCount == 0) throw new DnsClientException("The authenticated DNS response is missing TSIG.");
            var reader = new DnsWireReader(response);
            reader.Skip(4);
            ushort questions = reader.ReadUInt16();
            ushort answers = reader.ReadUInt16();
            ushort authorities = reader.ReadUInt16();
            reader.Skip(2);
            for (int i = 0; i < questions; i++) { reader.ReadName(); reader.Skip(4); }
            SkipRecords(reader, answers);
            SkipRecords(reader, authorities);

            int tsigStart = -1;
            string tsigName = string.Empty;
            ushort tsigClass = 0;
            uint tsigTtl = 0;
            int rdataOffset = 0;
            ushort rdataLength = 0;
            for (int i = 0; i < additionalCount; i++) {
                int recordStart = reader.Position;
                string owner = reader.ReadName();
                ushort type = reader.ReadUInt16();
                ushort recordClass = reader.ReadUInt16();
                uint ttl = reader.ReadUInt32();
                ushort length = reader.ReadUInt16();
                int offset = reader.Position;
                reader.Skip(length);
                if (type == TsigType) {
                    if (i != additionalCount - 1 || tsigStart >= 0) throw new DnsClientException("TSIG must be the final additional record.");
                    tsigStart = recordStart;
                    tsigName = owner;
                    tsigClass = recordClass;
                    tsigTtl = ttl;
                    rdataOffset = offset;
                    rdataLength = length;
                }
            }
            if (!reader.IsAtEnd || tsigStart < 0) throw new DnsClientException("The authenticated DNS response has an invalid additional section.");
            if (tsigClass != AnyClass || tsigTtl != 0 || !string.Equals(DnsWireNameCodec.Canonical(tsigName), DnsWireNameCodec.Canonical(key.Name), StringComparison.Ordinal)) {
                throw new DnsClientException("The TSIG response key name, class, or TTL is invalid.");
            }

            var tsig = new DnsWireReader(response, rdataOffset, rdataOffset + rdataLength);
            string algorithm = tsig.ReadName();
            ulong time = ReadUInt48(tsig);
            ushort fudge = tsig.ReadUInt16();
            byte[] responseMac = tsig.ReadBytes(tsig.ReadUInt16());
            ushort originalId = tsig.ReadUInt16();
            ushort error = tsig.ReadUInt16();
            byte[] other = tsig.ReadBytes(tsig.ReadUInt16());
            if (!tsig.IsAtEnd) throw new DnsClientException("TSIG RDATA contains trailing bytes.");
            if (!string.Equals(DnsWireNameCodec.Canonical(algorithm), DnsWireNameCodec.Canonical(AlgorithmName(key.Algorithm)), StringComparison.Ordinal)) {
                throw new DnsClientException("The TSIG response uses a different HMAC algorithm.");
            }
            if (originalId != expectedId) throw new DnsClientException("The TSIG original transaction ID does not match the update request.");
            long now = UtcNow().ToUnixTimeSeconds();
            if (Math.Abs(now - checked((long)time)) > fudge) throw new DnsClientException("The TSIG response time is outside the allowed fudge interval.");

            var messageWithoutTsig = new byte[tsigStart];
            Buffer.BlockCopy(response, 0, messageWithoutTsig, 0, tsigStart);
            WriteUInt16(messageWithoutTsig, 0, originalId);
            WriteUInt16(messageWithoutTsig, 10, checked((ushort)(additionalCount - 1)));
            using var macInput = new MemoryStream();
            WriteUInt16(macInput, checked((ushort)requestMac.Length));
            macInput.Write(requestMac, 0, requestMac.Length);
            macInput.Write(messageWithoutTsig, 0, messageWithoutTsig.Length);
            byte[] variables = BuildVariables(key.Name, algorithm, time, fudge, error, other);
            macInput.Write(variables, 0, variables.Length);
            byte[] expected = ComputeMac(key, macInput.ToArray());
            if (!FixedTimeEquals(expected, responseMac)) throw new DnsClientException("The TSIG response MAC is invalid.");
            if (error != 0) throw new DnsClientException($"The DNS server returned authenticated TSIG error {error}.");
        }

        private static byte[] BuildVariables(string keyName, string algorithmName, ulong time, ushort fudge, ushort error, byte[] other) {
            using var stream = new MemoryStream();
            WriteName(stream, keyName);
            WriteUInt16(stream, AnyClass);
            WriteUInt32(stream, 0);
            WriteName(stream, algorithmName);
            WriteUInt48(stream, time);
            WriteUInt16(stream, fudge);
            WriteUInt16(stream, error);
            WriteUInt16(stream, checked((ushort)other.Length));
            stream.Write(other, 0, other.Length);
            return stream.ToArray();
        }

        private static byte[] ComputeMac(TsigKey key, byte[] value) {
            byte[] secret = key.GetSecret();
            try {
                using HMAC hmac = key.Algorithm switch {
                    TsigAlgorithm.HmacSha1 => new HMACSHA1(secret),
                    TsigAlgorithm.HmacSha384 => new HMACSHA384(secret),
                    TsigAlgorithm.HmacSha512 => new HMACSHA512(secret),
                    _ => new HMACSHA256(secret)
                };
                return hmac.ComputeHash(value);
            } finally {
                Array.Clear(secret, 0, secret.Length);
            }
        }

        private static string AlgorithmName(TsigAlgorithm algorithm) => algorithm switch {
            TsigAlgorithm.HmacSha1 => "hmac-sha1.",
            TsigAlgorithm.HmacSha384 => "hmac-sha384.",
            TsigAlgorithm.HmacSha512 => "hmac-sha512.",
            _ => "hmac-sha256."
        };

        private static void SkipRecords(DnsWireReader reader, int count) {
            for (int i = 0; i < count; i++) { reader.ReadName(); reader.Skip(8); reader.Skip(reader.ReadUInt16()); }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right) {
            int minimum = Math.Max(10, left.Length / 2);
            if (right.Length < minimum || right.Length > left.Length) return false;
            int difference = 0;
            for (int i = 0; i < right.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private static byte[] Combine(byte[] left, byte[] right) {
            var value = new byte[left.Length + right.Length];
            Buffer.BlockCopy(left, 0, value, 0, left.Length);
            Buffer.BlockCopy(right, 0, value, left.Length, right.Length);
            return value;
        }

        private static void WriteName(Stream stream, string name) {
            byte[] wire = DnsWireNameCodec.ToCanonicalWire(name);
            stream.Write(wire, 0, wire.Length);
        }

        private static ushort ReadUInt16(byte[] value, int offset) => (ushort)((value[offset] << 8) | value[offset + 1]);
        private static void WriteUInt16(byte[] value, int offset, ushort item) { value[offset] = (byte)(item >> 8); value[offset + 1] = (byte)item; }
        private static void WriteUInt16(Stream stream, ushort value) { stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
        private static void WriteUInt32(Stream stream, uint value) { stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
        private static void WriteUInt48(Stream stream, ulong value) { for (int shift = 40; shift >= 0; shift -= 8) stream.WriteByte((byte)(value >> shift)); }
        private static ulong ReadUInt48(DnsWireReader reader) { ulong value = 0; for (int i = 0; i < 6; i++) value = (value << 8) | reader.ReadByte(); return value; }
    }
}
