namespace DnsClientX;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a parsed DMARC TXT record.
/// </summary>
public sealed class DmarcRecord {
    /// <summary>Gets the record tags.</summary>
    public IReadOnlyDictionary<string, string> Tags { get; }

    /// <summary>Initializes a new instance of the <see cref="DmarcRecord"/> class.</summary>
    /// <param name="tags">Tags parsed from the record.</param>
    public DmarcRecord(IReadOnlyDictionary<string, string> tags) => Tags = tags;

    /// <summary>Attempts to parse a DMARC record.</summary>
    /// <param name="record">Raw TXT record.</param>
    /// <param name="result">Parsed record.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    public static bool TryParse(string record, out DmarcRecord? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(record) || !record.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        var tags = record.Split(';')
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Select(t => t.Split('=', 2))
            .Where(parts => parts.Length >= 1)
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : string.Empty, StringComparer.OrdinalIgnoreCase);
        result = new DmarcRecord(tags);
        return true;
    }
}
