namespace DnsClientX;
using System;
using System.Collections.Generic;

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
        if (!DnsTxtTags.TryParse(record, "DMARC1", out var tags)) return false;
        result = new DmarcRecord(tags);
        return true;
    }
}
