using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DnsClientX;

public partial class DnsResponse : IJsonOnDeserialized {
    private string[]? _commentEntries;

    // Keep JSON entry boundaries for diagnostics while retaining the public joined string.
    // Internal visibility lets the source-generated context use the same mapping as reflection.
    [JsonInclude]
    [JsonPropertyName("Comment")]
    internal JsonElement? JsonComments {
        get => Comments == null ? null : JsonSerializer.SerializeToElement(Comments, DnsJsonContext.Default.String);
        set {
            if (!value.HasValue || value.Value.ValueKind == JsonValueKind.Null) {
                _commentEntries = null;
                Comments = null!;
                return;
            }
            _commentEntries = value.Value.ValueKind switch {
                JsonValueKind.String => new[] { value.Value.GetString()! },
                JsonValueKind.Array => value.Value.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.String)
                    .Select(entry => entry.GetString()!).ToArray(),
                _ => throw new JsonException("DNS comments must be a string or an array of strings.")
            };
            Comments = string.Join("; ", _commentEntries);
        }
    }

    /// <inheritdoc />
    void IJsonOnDeserialized.OnDeserialized() {
        string[]? commentEntries = _commentEntries;
        _commentEntries = null;
        ExtendedDnsErrors ??= Array.Empty<ExtendedDnsError>();
        // Structured provider data is authoritative; comments remain available verbatim.
        if (ExtendedDnsErrors.Length != 0 || string.IsNullOrEmpty(Comments)) { return; }

        var errors = new List<ExtendedDnsError>();
        foreach (string comment in commentEntries ?? new[] { Comments }) {
            if (TryReadEdeHeader(comment, 0, out ushort code, out int textStart)) {
                errors.Add(new ExtendedDnsError { InfoCode = code, ExtraText = comment.Substring(textStart).Trim() });
            }
        }
        ExtendedDnsErrors = errors.ToArray();
    }

    private static bool TryReadEdeHeader(string text, int start, out ushort code, out int textStart) {
        code = 0;
        textStart = 0;
        if (text.Length - start < 4 || string.CompareOrdinal(text, start, "EDE(", 0, 4) != 0) { return false; }
        int close = text.IndexOf(')', start + 4);
        if (close <= start + 4 || close + 1 >= text.Length || text[close + 1] != ':' ||
            !ushort.TryParse(text.Substring(start + 4, close - start - 4), NumberStyles.None, CultureInfo.InvariantCulture, out code)) { return false; }
        textStart = close + 2;
        return true;
    }
}
