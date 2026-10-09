using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DnsClientX;

/// <summary>Owns SVCB parameter validation and decoding for all query transports.</summary>
internal static partial class DnsSvcbCodec {
    internal static string KeyName(ushort key) => key switch {
        0 => "mandatory", 1 => "alpn", 2 => "no-default-alpn", 3 => "port",
        4 => "ipv4hint", 5 => "ech", 6 => "ipv6hint", 7 => "dohpath", 8 => "ohttp",
        _ => "key" + key.ToString(CultureInfo.InvariantCulture)
    };

    internal static bool TryKey(string name, out ushort key, out bool numeric) {
        numeric = name.StartsWith("key", StringComparison.Ordinal);
        if (numeric) return ushort.TryParse(name.Substring(3), NumberStyles.None, CultureInfo.InvariantCulture, out key)
            && name == "key" + key.ToString(CultureInfo.InvariantCulture);
        for (ushort known = 0; known <= 8; known++) {
            if (name == KeyName(known)) { key = known; return true; }
        }
        key = 0;
        return false;
    }

    internal static SvcbRecord Read(DnsWireReader reader) {
        try {
            ushort priority = reader.ReadUInt16();
            // RFC 9460 prohibits compression in TargetName, unlike most DNS name fields.
            int offset = reader.Position;
            while (true) {
                if (offset >= reader.End) throw new DnsClientException("SVCB target name is truncated.");
                int length = reader.Message[offset++];
                if (length > 63 || offset + length > reader.End) throw new DnsClientException("SVCB target name must be uncompressed and complete.");
                if (length == 0) break;
                offset += length;
            }
            string target = reader.ReadName();
            var parameters = new List<SvcbParameter>();
            ushort? previous = null;
            while (!reader.IsAtEnd) {
                ushort key = reader.ReadUInt16();
                ushort length = reader.ReadUInt16();
                if (previous.HasValue && key <= previous.Value) throw new DnsClientException("SVCB parameters are not in strictly increasing key order.");
                previous = key;
                parameters.Add(new SvcbParameter(key, reader.ReadBytes(length)));
            }
            return new SvcbRecord(priority, target, parameters);
        } catch (ArgumentException exception) {
            throw new DnsClientException("SVCB RDATA contains an invalid parameter or target name.", exception);
        }
    }

    internal static void ValidateParameter(SvcbParameter parameter) {
        if (parameter.Key == ushort.MaxValue) throw new ArgumentException("SVCB parameter key 65535 is reserved.");
        byte[] value = parameter.Bytes;
        bool valid = parameter.Key switch {
            0 => value.Length > 0 && value.Length % 2 == 0,
            1 => value.Length > 0,
            2 or 8 => value.Length == 0,
            3 => value.Length == 2,
            4 => value.Length > 0 && value.Length % 4 == 0,
            6 => value.Length > 0 && value.Length % 16 == 0,
            _ => true
        };
        if (!valid) throw new ArgumentException("SVCB " + parameter.Name + " parameter has an invalid length.");
        if (parameter.Key == 0) {
            ushort previous = 0;
            foreach (ushort key in ReadKeys(value)) {
                if (key <= previous) throw new ArgumentException("SVCB mandatory keys must be nonzero and strictly increasing.");
                previous = key;
            }
        }
        if (parameter.Key == 1) ReadAlpn(value);
    }

    internal static void ValidateRecord(SvcbRecord record) {
        if (2L + DnsWireNameCodec.ToCanonicalWire(record.Target).Length
            + record.Parameters.Values.Sum(parameter => 4L + parameter.Bytes.Length) > ushort.MaxValue)
            throw new ArgumentException("SVCB RDATA exceeds the wire length limit.");
        if (record.IsAliasMode) return;
        if (record.MandatoryKeys.Any(key => !record.Parameters.ContainsKey(key)))
            throw new ArgumentException("SVCB mandatory parameter refers to a missing key.");
        if (record.NoDefaultAlpn && !record.Parameters.ContainsKey(1))
            throw new ArgumentException("SVCB no-default-alpn requires an alpn parameter.");
    }

    internal static ushort[] ReadKeys(byte[] bytes) {
        var keys = new ushort[bytes.Length / 2];
        for (int index = 0; index < keys.Length; index++) keys[index] = (ushort)((bytes[index * 2] << 8) | bytes[index * 2 + 1]);
        return keys;
    }

    internal static string[] ReadAlpn(byte[] bytes) {
        var protocols = new List<string>();
        for (int offset = 0; offset < bytes.Length;) {
            int length = bytes[offset++];
            if (length == 0 || offset + length > bytes.Length) throw new ArgumentException("SVCB alpn value contains an empty or truncated identifier.");
            var identifier = new char[length];
            for (int index = 0; index < length; index++) identifier[index] = (char)bytes[offset + index];
            protocols.Add(new string(identifier));
            offset += length;
        }
        return protocols.ToArray();
    }
}
