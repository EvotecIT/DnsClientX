namespace DnsClientX;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a TXT record containing key/value tags.
/// </summary>
public sealed class KeyValueTxtRecord {
    /// <summary>Gets the record tags.</summary>
    public TagRecord[] Tags { get; }

    /// <summary>Gets the count of tags in this record.</summary>
    public int TagCount => Tags.Length;

    /// <summary>Gets a summary of all tags for display purposes.</summary>
    public string TagSummary => string.Join("; ", Tags.Select(t => $"{t.Tag}={t.Value}"));

    /// <summary>Initializes a new instance of the <see cref="KeyValueTxtRecord"/> class.</summary>
    /// <param name="tags">Tags parsed from the record.</param>
    public KeyValueTxtRecord(TagRecord[] tags) => Tags = tags;

    /// <summary>Initializes a new instance of the <see cref="KeyValueTxtRecord"/> class.</summary>
    /// <param name="tags">Tags parsed from the record.</param>
    public KeyValueTxtRecord(IReadOnlyDictionary<string, string> tags) =>
        Tags = tags.Select(kvp => new TagRecord(kvp.Key, kvp.Value)).ToArray();

    /// <summary>Gets a tag value by key.</summary>
    /// <param name="key">The tag key to look up.</param>
    /// <returns>The tag value, or null if not found.</returns>
    public string? GetTagValue(string key) =>
        Tags.FirstOrDefault(t => string.Equals(t.Tag, key, StringComparison.OrdinalIgnoreCase))?.Value;

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
        var tags = new List<TagRecord>();
        foreach (var part in parts) {
            var kv = part.Split(new[] { '=' }, 2);
            if (kv.Length != 2) {
                return false;
            }
            tags.Add(new TagRecord(kv[0], kv[1]));
        }
        if (tags.Count == 0) {
            return false;
        }
        result = new KeyValueTxtRecord(tags.ToArray());
        return true;
    }
}