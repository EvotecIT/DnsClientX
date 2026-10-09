namespace DnsClientX;
using System;
using System.Collections.Generic;

/// <summary>
/// Represents a parsed DKIM TXT record.
/// </summary>
public sealed class DkimRecord {
    /// <summary>Gets the record tags.</summary>
    public IReadOnlyDictionary<string, string> Tags { get; }

    /// <summary>Initializes a new instance of the <see cref="DkimRecord"/> class.</summary>
    /// <param name="tags">Tags parsed from the record.</param>
    public DkimRecord(IReadOnlyDictionary<string, string> tags) => Tags = tags;

    /// <summary>Attempts to parse a DKIM record.</summary>
    /// <param name="record">Raw TXT record.</param>
    /// <param name="result">Parsed record.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    public static bool TryParse(string record, out DkimRecord? result) {
        result = null;
        if (!DnsTxtTags.TryParse(record, "DKIM1", out var tags)) return false;
        result = new DkimRecord(tags);
        return true;
    }
}
