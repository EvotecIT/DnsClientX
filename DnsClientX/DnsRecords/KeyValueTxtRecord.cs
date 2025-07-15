namespace DnsClientX;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a TXT record containing key/value tags.
/// </summary>
public sealed class KeyValueTxtRecord {
    /// <summary>Gets the record tags.</summary>
    public IReadOnlyDictionary<string, string> Tags { get; }

    /// <summary>Initializes a new instance of the <see cref="KeyValueTxtRecord"/> class.</summary>
    /// <param name="tags">Tags parsed from the record.</param>
    public KeyValueTxtRecord(IReadOnlyDictionary<string, string> tags) => Tags = tags;

    /// <summary>Attempts to parse a key/value TXT record.</summary>
    /// <param name="record">Raw TXT record.</param>
    /// <param name="result">Parsed record.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    public static bool TryParse(string record, out KeyValueTxtRecord? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(record)) {
            return false;
        }
        var parts = record.Split(new[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in parts) {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) {
                return false;
            }
            tags[kv[0]] = kv[1];
        }
        if (tags.Count == 0) {
            return false;
        }
        result = new KeyValueTxtRecord(tags);
        return true;
    }
}
