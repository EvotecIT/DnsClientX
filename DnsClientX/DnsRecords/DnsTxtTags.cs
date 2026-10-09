using System;
using System.Collections.Generic;

namespace DnsClientX;

/// <summary>Reads versioned TXT tag lists without throwing on malformed or duplicate fields.</summary>
internal static class DnsTxtTags {
    internal static bool TryParse(string record, string version, out Dictionary<string, string> tags) {
        tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(record)) return false;
        foreach (string field in record.Split(';')) {
            string trimmed = field.Trim();
            if (trimmed.Length == 0) continue;
            string[] pair = trimmed.Split(new[] { '=' }, 2);
            if (pair.Length != 2) return false;
            string name = pair[0].Trim();
            if (name.Length == 0 || tags.ContainsKey(name)) return false;
            if (tags.Count == 0 && !string.Equals(name, "v", StringComparison.OrdinalIgnoreCase)) return false;
            tags.Add(name, pair[1].Trim());
        }
        return tags.TryGetValue("v", out string? value) && string.Equals(value, version, StringComparison.OrdinalIgnoreCase);
    }
}
