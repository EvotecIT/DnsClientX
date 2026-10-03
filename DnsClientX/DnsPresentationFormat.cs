using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DnsClientX;

/// <summary>Shared DNS presentation escaping and quoted-field parsing.</summary>
internal static class DnsPresentationFormat {
    internal readonly record struct Token(string Raw, string Value);

    /// <summary>Decodes RFC 3597 RDATA only when its hexadecimal words and declared length agree.</summary>
    internal static bool TryDecodeRfc3597(string text, out byte[] data) {
        data = Array.Empty<byte>();
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words[0] != "\\#"
            || !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int length)
            || length < 0 || length > ushort.MaxValue) return false;
        var bytes = new byte[length];
        int offset = 0;
        for (int word = 2; word < words.Length; word++) {
            string hex = words[word];
            if (hex.Length % 2 != 0) return false;
            for (int index = 0; index < hex.Length; index += 2) {
                if (offset >= length) return false;
                int high = HexDigit(hex[index]);
                int low = HexDigit(hex[index + 1]);
                if (high < 0 || low < 0) return false;
                bytes[offset++] = (byte)((high << 4) | low);
            }
        }
        if (offset != length) return false;
        data = bytes;
        return true;
    }

    private static int HexDigit(char value) => value >= '0' && value <= '9' ? value - '0'
        : value >= 'a' && value <= 'f' ? value - 'a' + 10
        : value >= 'A' && value <= 'F' ? value - 'A' + 10 : -1;

    // Values retain escapes: DNS names and character-strings have different decoding rules.
    internal static List<Token> Tokenize(string text, out bool complete) {
        var tokens = new List<Token>();
        var raw = new StringBuilder();
        var value = new StringBuilder();
        bool quoted = false;
        for (int index = 0; index < text.Length; index++) {
            char current = text[index];
            if (char.IsWhiteSpace(current) && !quoted) {
                AddToken();
            } else if (current == '"') {
                quoted = !quoted;
                raw.Append(current);
            } else if (current == '\\' && index + 1 < text.Length) {
                raw.Append(current).Append(text[++index]);
                value.Append(current).Append(text[index]);
            } else {
                raw.Append(current);
                value.Append(current);
            }
        }
        AddToken();
        complete = !quoted;
        return tokens;

        void AddToken() {
            if (raw.Length == 0) return;
            tokens.Add(new Token(raw.ToString(), value.ToString()));
            raw.Clear();
            value.Clear();
        }
    }

    internal static string[] TxtStrings(string? text, bool decoded) {
        if (text == null) return Array.Empty<string>();
        if (!TryReadTxtChunk(text, 0, out int start, out int end, out int next)) {
            return new[] { decoded ? Unescape(text) : text };
        }
        var values = new List<string>();
        do {
            values.Add(decoded ? Unescape(text.Substring(start + 1, end - start - 2)) : text.Substring(start, end - start));
            if (next == text.Length) return values.ToArray();
        } while (TryReadTxtChunk(text, next, out start, out end, out next));
        // Preserve an unrecognized provider presentation rather than dropping part of it.
        return new[] { text };
    }

    internal static string ConcatenateTxt(string text) {
        if (!TryReadTxtChunk(text, 0, out int start, out int end, out int next)) return Unescape(text);
        string first = Unescape(text.Substring(start + 1, end - start - 2));
        if (next == text.Length) return first;
        var builder = new StringBuilder(text.Length);
        builder.Append(first);
        do {
            if (!TryReadTxtChunk(text, next, out start, out end, out next)) return text;
            AppendUnescaped(builder, text, start + 1, end - start - 2);
        } while (next < text.Length);
        return builder.ToString();
    }

    private static bool TryReadTxtChunk(string text, int index, out int start, out int end, out int next) {
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        start = index;
        end = next = index;
        if (index == text.Length || text[index++] != '"') return false;
        while (index < text.Length) {
            char current = text[index++];
            if (current == '\\' && index < text.Length) {
                index++; // An escaped quote does not terminate the character-string.
            } else if (current == '"') {
                end = index;
                while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
                next = index;
                return true;
            }
        }
        return false;
    }

    internal static string Unescape(string value) {
        if (value.IndexOf('\\') < 0) return value;
        var builder = new StringBuilder(value.Length);
        AppendUnescaped(builder, value, 0, value.Length);
        return builder.ToString();
    }

    private static void AppendUnescaped(StringBuilder builder, string value, int offset, int length) {
        int end = offset + length;
        for (int index = offset; index < end; index++) {
            if (value[index] != '\\' || index + 1 >= end) {
                builder.Append(value[index]);
                continue;
            }
            if (index + 3 < end && IsDigit(value[index + 1]) && IsDigit(value[index + 2]) && IsDigit(value[index + 3])) {
                int octet = (value[index + 1] - '0') * 100 + (value[index + 2] - '0') * 10 + value[index + 3] - '0';
                if (octet <= byte.MaxValue) {
                    builder.Append((char)octet);
                    index += 3;
                    continue;
                }
            }
            builder.Append(value[++index]);
        }
    }

    private static bool IsDigit(char value) => value >= '0' && value <= '9';

    internal static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
