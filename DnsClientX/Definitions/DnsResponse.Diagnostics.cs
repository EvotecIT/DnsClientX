using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace DnsClientX;

public partial class DnsResponse : IJsonOnDeserialized {
    /// <inheritdoc />
    void IJsonOnDeserialized.OnDeserialized() {
        ExtendedDnsErrors ??= Array.Empty<ExtendedDnsError>();
        // Structured provider data is authoritative; comments remain available verbatim.
        if (ExtendedDnsErrors.Length != 0 || string.IsNullOrEmpty(Comments)) return;

        var errors = new List<ExtendedDnsError>();
        int start = 0;
        while (start < Comments.Length) {
            // CommentConverter joins Cloudflare's comment array with this separator.
            int next = Comments.IndexOf("; EDE(", start, StringComparison.Ordinal);
            while (next >= 0 && !TryReadEdeHeader(Comments, next + 2, out _, out _)) {
                next = Comments.IndexOf("; EDE(", next + 2, StringComparison.Ordinal);
            }
            if (TryReadEdeHeader(Comments, start, out ushort code, out int textStart)) {
                int textEnd = next < 0 ? Comments.Length : next;
                errors.Add(new ExtendedDnsError { InfoCode = code, ExtraText = Comments.Substring(textStart, textEnd - textStart).Trim() });
            }
            if (next < 0) break;
            start = next + 2;
        }
        ExtendedDnsErrors = errors.ToArray();
    }

    private static bool TryReadEdeHeader(string text, int start, out ushort code, out int textStart) {
        code = 0;
        textStart = 0;
        if (text.Length - start < 4 || string.CompareOrdinal(text, start, "EDE(", 0, 4) != 0) return false;
        int close = text.IndexOf(')', start + 4);
        if (close <= start + 4 || close + 1 >= text.Length || text[close + 1] != ':' ||
            !ushort.TryParse(text.Substring(start + 4, close - start - 4), NumberStyles.None, CultureInfo.InvariantCulture, out code)) return false;
        textStart = close + 2;
        return true;
    }
}
