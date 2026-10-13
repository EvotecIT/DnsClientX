using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace DnsClientX.DnsCrypt;

internal static class DnsCryptWire {
    private static readonly byte[] ResponseMagic = { 0x72, 0x36, 0x66, 0x6e, 0x76, 0x57, 0x6a, 0x38 };

    internal static void ValidateQuery(byte[] query) {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (query.Length > 4027) throw new ArgumentException("The DNS wire query leaves insufficient room for DNSCrypt framing and padding.", nameof(query));
        if (!DnsWireMessageParser.TryParseHeader(query, out var header) || header.IsResponse || header.OperationCode != 0 || header.QuestionCount != 1
            || !DnsWireMessageParser.TryParseQuestion(query, 0, out _) || !DnsWireMessageParser.TryParseEdns(query, out _)) {
            throw new ArgumentException("DNSCrypt requires a complete DNS query with exactly one question.", nameof(query));
        }
    }

    internal static bool Matches(byte[] query, byte[] response) {
        return DnsWireMessageParser.TryParseHeader(response, out var header) && header.IsResponse && header.QuestionCount == 1
            && DnsWireMessageParser.TryParseHeader(query, out var request) && header.TransactionId == request.TransactionId && header.OperationCode == request.OperationCode
            && DnsWireMessageParser.TryParseQuestion(query, 0, out var expected) && DnsWireMessageParser.TryParseQuestion(response, 0, out var actual)
            && expected.Type == actual.Type && expected.Class == actual.Class && string.Equals(expected.Name, actual.Name, StringComparison.OrdinalIgnoreCase);
    }

    internal static byte[] CertificateQuery(string name, int targetSize = 512) {
        byte[] initial = new DnsMessage(name, DnsRecordType.TXT, false).SerializeDnsWireFormat();
        // Padding makes certificate discovery usable through relays that limit the response
        // to the initiating request size. This query contains no application question.
        int padding = Math.Max(0, targetSize - initial.Length - 15);
        var result = new byte[initial.Length + 15 + padding];
        Buffer.BlockCopy(initial, 0, result, 0, initial.Length);
        result[11] = 1;
        int offset = initial.Length;
        result[offset++] = 0; WriteUInt16(result, ref offset, 41); WriteUInt16(result, ref offset, 4096);
        offset += 4; WriteUInt16(result, ref offset, (ushort)(padding + 4));
        WriteUInt16(result, ref offset, 12); WriteUInt16(result, ref offset, (ushort)padding);
        return result;
    }

    internal static IReadOnlyList<byte[]> CertificateRecords(byte[] query, byte[] response) {
        if (!Matches(query, response) || !DnsWireMessageParser.TryParseHeader(response, out var header) || header.IsTruncated || header.ResponseCode != DnsResponseCode.NoError) {
            throw new InvalidDataException("The certificate discovery response is mismatched, truncated, or unsuccessful.");
        }
        var records = new List<byte[]>();
        try {
            var reader = new DnsWireReader(response, 12);
            string provider = reader.ReadName(); reader.Skip(4);
            for (int i = 0; i < header.AnswerCount + header.AuthorityCount + header.AdditionalCount; i++) {
                string owner = reader.ReadName(); ushort type = reader.ReadUInt16(), recordClass = reader.ReadUInt16(); reader.Skip(4);
                byte[] data = reader.ReadBytes(reader.ReadUInt16());
                if (i >= header.AnswerCount || type != 16 || recordClass != 1 || !string.Equals(owner, provider, StringComparison.OrdinalIgnoreCase)) continue;
                using var certificate = new MemoryStream();
                for (int offset = 0; offset < data.Length;) {
                    int length = data[offset++];
                    if (length > data.Length - offset) throw new InvalidDataException("A certificate TXT character-string is truncated.");
                    certificate.Write(data, offset, length); offset += length;
                }
                records.Add(certificate.ToArray());
            }
            if (!reader.IsAtEnd) throw new InvalidDataException("The certificate response contains trailing bytes.");
        } catch (DnsClientException exception) {
            throw new InvalidDataException("The certificate DNS response is malformed.", exception);
        }
        return records;
    }

    internal static byte[] Pad(byte[] dns, int plaintextLength) {
        if (plaintextLength <= dns.Length || plaintextLength > 4028) throw new ArgumentOutOfRangeException(nameof(plaintextLength));
        var padded = new byte[plaintextLength]; Buffer.BlockCopy(dns, 0, padded, 0, dns.Length); padded[dns.Length] = 0x80; return padded;
    }

    internal static byte[] Unpad(byte[] padded) {
        int end = padded.Length - 1;
        while (end >= 0 && padded[end] == 0) end--;
        if (end < 12 || padded[end] != 0x80) throw new InvalidDataException("DNSCrypt response padding is invalid.");
        return DnsCryptCertificate.Slice(padded, 0, end);
    }

    internal static byte[] EncryptQuery(DnsCryptCertificate certificate, byte[] query, byte[] clientPublicKey, byte[] sharedKey, byte[] clientNonce, int plaintextLength) {
        var nonce = new byte[24]; Buffer.BlockCopy(clientNonce, 0, nonce, 0, 12);
        byte[] encrypted = DnsCryptSodium.Encrypt(certificate.EncryptionSystem, Pad(query, plaintextLength), nonce, sharedKey);
        var packet = new byte[52 + encrypted.Length];
        Buffer.BlockCopy(certificate.ClientMagic, 0, packet, 0, 8); Buffer.BlockCopy(clientPublicKey, 0, packet, 8, 32);
        Buffer.BlockCopy(clientNonce, 0, packet, 40, 12); Buffer.BlockCopy(encrypted, 0, packet, 52, encrypted.Length);
        return packet;
    }

    internal static byte[] DecryptResponse(DnsCryptCertificate certificate, byte[] query, byte[] packet, byte[] sharedKey, byte[] clientNonce) {
        if (packet.Length < 61 || packet.Length >= 4096 || !packet.Take(8).SequenceEqual(ResponseMagic)
            || !packet.Skip(8).Take(12).SequenceEqual(clientNonce) || packet.Skip(20).Take(12).All(value => value == 0)) {
            throw new InvalidDataException("The DNSCrypt response has invalid framing or nonce correlation.");
        }
        byte[] padded = DnsCryptSodium.Decrypt(certificate.EncryptionSystem, DnsCryptCertificate.Slice(packet, 32, packet.Length - 32), DnsCryptCertificate.Slice(packet, 8, 24), sharedKey);
        byte[] dns = Unpad(padded);
        if (!Matches(query, dns) || !DnsWireMessageParser.TryParseEdns(dns, out _)) throw new InvalidDataException("The authenticated DNSCrypt response does not match the DNS query or is malformed.");
        return dns;
    }

    internal static byte[] WrapRelay(byte[] inner, IPEndPoint? resolver) {
        if (resolver == null) return inner;
        var packet = new byte[28 + inner.Length];
        for (int i = 0; i < 8; i++) packet[i] = 255;
        byte[] address = resolver.Address.MapToIPv6().GetAddressBytes(); Buffer.BlockCopy(address, 0, packet, 10, 16);
        int offset = 26; WriteUInt16(packet, ref offset, (ushort)resolver.Port); Buffer.BlockCopy(inner, 0, packet, 28, inner.Length); return packet;
    }

    private static void WriteUInt16(byte[] bytes, ref int offset, ushort value) { bytes[offset++] = (byte)(value >> 8); bytes[offset++] = (byte)value; }
}
