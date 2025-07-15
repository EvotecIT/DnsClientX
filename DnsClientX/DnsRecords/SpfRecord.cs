namespace DnsClientX;
using System;
using System.Collections.Generic;

/// <summary>
/// Represents a parsed SPF TXT record.
/// </summary>
public sealed class SpfRecord {
    /// <summary>Gets the SPF mechanisms.</summary>
    public IReadOnlyList<string> Mechanisms { get; }

    /// <summary>Initializes a new instance of the <see cref="SpfRecord"/> class.</summary>
    /// <param name="mechanisms">Mechanisms parsed from the record.</param>
    public SpfRecord(IReadOnlyList<string> mechanisms) => Mechanisms = mechanisms;

    /// <summary>Attempts to parse an SPF record.</summary>
    /// <param name="record">Raw TXT record.</param>
    /// <param name="result">Parsed record.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    public static bool TryParse(string record, out SpfRecord? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(record) || !record.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        var mechanisms = record.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        result = new SpfRecord(mechanisms);
        return true;
    }
}
