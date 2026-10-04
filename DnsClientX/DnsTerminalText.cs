using System;
using System.Globalization;
using System.Text;

namespace DnsClientX;

/// <summary>Escapes untrusted text only when presenting it to a human-readable terminal.</summary>
internal static class DnsTerminalText {
    internal static string Escape(string? value) {
        if (value == null) return string.Empty;
        if (value.Length == 0) return value;

        int first = 0;
        while (first < value.Length && !NeedsEscape(value, first)) first++;
        if (first == value.Length) return value;

        var builder = new StringBuilder(value.Length + 6);
        builder.Append(value, 0, first);
        for (int index = first; index < value.Length; index++) {
            char current = value[index];
            if (NeedsEscape(value, index)) {
                AppendEscapedCodeUnit(builder, current);
                if (char.IsHighSurrogate(current) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])) {
                    AppendEscapedCodeUnit(builder, value[++index]);
                }
            } else {
                builder.Append(current);
            }
        }
        return builder.ToString();
    }

    private static void AppendEscapedCodeUnit(StringBuilder builder, char value) =>
        builder.Append("\\u").Append(((int)value).ToString("X4", CultureInfo.InvariantCulture));

    private static bool NeedsEscape(string text, int index) {
        char value = text[index];
        if (char.IsControl(value)) return true;
        if (value < 0x80) return false;
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(text, index);
        return category is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
    }
}
