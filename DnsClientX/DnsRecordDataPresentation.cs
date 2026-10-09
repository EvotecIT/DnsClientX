using System;
using System.Globalization;
using System.Linq;

namespace DnsClientX;

/// <summary>Normalizes recognized provider presentations while preserving raw and opaque data.</summary>
internal static class DnsRecordDataPresentation {
    internal static string[] TxtStrings(string? raw, DnsRecordType type, bool decoded) {
        if (raw == null) return Array.Empty<string>();
        string data = ExpandGeneric(raw, type);
        return data.TrimStart().StartsWith("\\#", StringComparison.Ordinal)
            ? new[] { raw } : DnsPresentationFormat.TxtStrings(data, decoded);
    }

    internal static string ExpandGeneric(string raw, DnsRecordType type) {
        if (!raw.TrimStart().StartsWith("\\#", StringComparison.Ordinal)
            || !DnsPresentationFormat.TryDecodeRfc3597(raw, out byte[] rdata)) return raw;
        try {
            string formatted = DnsWireRecordFormatter.Format(rdata, type, 0, checked((ushort)rdata.Length));
            // Unsupported types remain opaque, including the provider's original hex spelling.
            return formatted.StartsWith("\\#", StringComparison.Ordinal) ? raw : formatted;
        } catch (DnsClientException) {
            return raw;
        }
    }

    internal static string NormalizeName(string value) {
        if (value.IndexOf('\\') >= 0) {
            try {
                byte[] name = DnsWireNameCodec.ToCanonicalWire(value);
                return DnsWireNameCodec.TrimTrailingRootDot(DnsWireRecordFormatter.Format(name, DnsRecordType.PTR, 0, checked((ushort)name.Length)));
            } catch (ArgumentException) { }
        }
        return DnsWireNameCodec.TrimTrailingRootDot(value).ToLowerInvariant();
    }

    internal static string Names(string data, int fields, params int[] names) {
        var tokens = DnsPresentationFormat.Tokenize(data, out bool complete);
        if (!complete || tokens.Count != fields) return data;
        string[] values = tokens.Select(token => token.Raw).ToArray();
        foreach (int index in names) values[index] = NormalizeName(tokens[index].Value);
        for (int index = 0; index < fields; index++) {
            if (!names.Contains(index) && uint.TryParse(values[index], NumberStyles.None, CultureInfo.InvariantCulture, out uint number))
                values[index] = number.ToString(CultureInfo.InvariantCulture);
        }
        return string.Join(" ", values);
    }

    internal static string Caa(string data) {
        var tokens = DnsPresentationFormat.Tokenize(data, out bool complete);
        if (!complete || tokens.Count != 3 || !byte.TryParse(tokens[0].Value, out byte flags)) return data;
        return flags.ToString(CultureInfo.InvariantCulture) + " "
            + DnsPresentationFormat.Unescape(tokens[1].Value).ToLowerInvariant() + " "
            + DnsPresentationFormat.Quote(DnsPresentationFormat.Unescape(tokens[2].Value));
    }

    internal static string Encoded(string data, DnsRecordType type) {
        string[] parts = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return data;
        string payload = string.Concat(parts.Skip(3));
        if (type is DnsRecordType.DNSKEY or DnsRecordType.CDNSKEY) {
            if (!ushort.TryParse(parts[0], out ushort flags) || !byte.TryParse(parts[1], out byte protocol)
                || !TryAlgorithm(parts[2], out DnsKeyAlgorithm algorithm)) return data;
            return $"{flags} {protocol} {algorithm} {payload}";
        }
        if (type is DnsRecordType.DS or DnsRecordType.CDS or DnsRecordType.DLV or DnsRecordType.TA) {
            if (!ushort.TryParse(parts[0], out ushort keyTag) || !TryAlgorithm(parts[1], out DnsKeyAlgorithm algorithm)
                || !byte.TryParse(parts[2], out byte digestType) || !IsHex(payload)) return data;
            return $"{keyTag} {algorithm} {digestType} {payload.ToUpperInvariant()}";
        }
        if (!byte.TryParse(parts[0], out byte first) || !byte.TryParse(parts[1], out byte second)
            || !byte.TryParse(parts[2], out byte third) || !IsHex(payload)) return data;
        return $"{first} {second} {third} {payload.ToUpperInvariant()}";
    }

    private static bool TryAlgorithm(string value, out DnsKeyAlgorithm algorithm) =>
        Enum.TryParse(value, true, out algorithm) && (int)algorithm >= 0 && (int)algorithm <= byte.MaxValue;

    private static bool IsHex(string value) => value.Length % 2 == 0 && value.All(c =>
        c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F');

    internal static string Sshfp(string data) {
        string[] parts = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !byte.TryParse(parts[0], out byte algorithm) || !byte.TryParse(parts[1], out byte fingerprintType)) return data;
        string fingerprint = string.Concat(parts.Skip(2));
        return IsHex(fingerprint) ? $"{algorithm} {fingerprintType} {fingerprint.ToUpperInvariant()}" : data;
    }

    internal static string Nsec(string data) {
        string[] parts = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return data;
        parts[0] = NormalizeName(parts[0]);
        for (int index = 1; index < parts.Length; index++) {
            if (parts[index].StartsWith("TYPE", StringComparison.OrdinalIgnoreCase)
                && ushort.TryParse(parts[index].Substring(4), out ushort number)) {
                parts[index] = Enum.IsDefined(typeof(DnsRecordType), number)
                    ? ((DnsRecordType)number).ToString()
                    : "TYPE" + number.ToString(CultureInfo.InvariantCulture);
            }
        }
        return string.Join(" ", parts);
    }
}
