using System;
using System.IO;

namespace DnsClientX.Odoh;

/// <summary>Strict TLS-style length framing for RFC 9230 messages.</summary>
internal static class OdohEncoding {
    internal static int ReadUInt16(byte[] bytes, ref int offset, int end) {
        if (end - offset < 2) {
            throw new InvalidDataException("Truncated ODoH length field.");
        }
        int value = (bytes[offset] << 8) | bytes[offset + 1];
        offset += 2;
        return value;
    }

    internal static byte[] ReadVector(byte[] bytes, ref int offset, int end) {
        int length = ReadUInt16(bytes, ref offset, end);
        if (length > end - offset) {
            throw new InvalidDataException("Truncated ODoH vector.");
        }
        var value = new byte[length];
        Buffer.BlockCopy(bytes, offset, value, 0, length);
        offset += length;
        return value;
    }

    internal static void WriteUInt16(byte[] bytes, int offset, int value) {
        if ((uint)value > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(value), "ODoH vectors cannot exceed 65535 bytes.");
        }
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    internal static byte[] Vector(byte[] value) {
        var bytes = new byte[value.Length + 2];
        WriteUInt16(bytes, 0, value.Length);
        Buffer.BlockCopy(value, 0, bytes, 2, value.Length);
        return bytes;
    }

    internal static byte[] Concat(params byte[][] values) {
        int size = 0;
        foreach (byte[] value in values) {
            size = checked(size + value.Length);
        }
        var bytes = new byte[size];
        int offset = 0;
        foreach (byte[] value in values) {
            Buffer.BlockCopy(value, 0, bytes, offset, value.Length);
            offset += value.Length;
        }
        return bytes;
    }

    internal static byte[] EncodeMessage(byte type, byte[] keyId, byte[] ciphertext) {
        return Concat(new[] { type }, Vector(keyId), Vector(ciphertext));
    }

    internal static byte[] EncodePlaintext(byte[] dnsMessage, int blockSize) {
        int length = checked(dnsMessage.Length + 4);
        int padding = blockSize == 0 ? 0 : (blockSize - (length % blockSize)) % blockSize;
        if (length + padding + 32 + 16 > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(dnsMessage), "The padded ODoH query exceeds the encrypted message limit.");
        }
        var bytes = new byte[length + padding];
        WriteUInt16(bytes, 0, dnsMessage.Length);
        Buffer.BlockCopy(dnsMessage, 0, bytes, 2, dnsMessage.Length);
        WriteUInt16(bytes, dnsMessage.Length + 2, padding);
        return bytes;
    }

    internal static byte[] DecodePlaintext(byte[] bytes) {
        int offset = 0;
        byte[] dnsMessage = ReadVector(bytes, ref offset, bytes.Length);
        if (dnsMessage.Length == 0) {
            throw new InvalidDataException("An ODoH DNS message cannot be empty.");
        }
        byte[] padding = ReadVector(bytes, ref offset, bytes.Length);
        if (offset != bytes.Length) {
            throw new InvalidDataException("Trailing ODoH plaintext bytes.");
        }
        int invalid = 0;
        foreach (byte value in padding) {
            invalid |= value;
        }
        if (invalid != 0) {
            throw new InvalidDataException("ODoH padding must contain only zeros.");
        }
        return dnsMessage;
    }
}
