namespace DnsClientX;
using System;
using System.Collections.Generic;

/// <summary>
/// Represents a parsed SPF TXT record.
/// </summary>
public sealed class SpfRecord {
    /// <summary>Gets the SPF version token.</summary>
    public string Version { get; }

    /// <summary>Gets the SPF mechanisms.</summary>
    public IReadOnlyList<string> Mechanisms { get; }

    /// <summary>Initializes a new instance of the <see cref="SpfRecord"/> class.</summary>
    /// <param name="version">The version token (e.g. <c>"v=spf1"</c>).</param>
    /// <param name="mechanisms">Mechanisms parsed from the record.</param>
    public SpfRecord(string version, IReadOnlyList<string> mechanisms) {
        Version = version;
        Mechanisms = mechanisms;
    }

    /// <summary>Attempts to parse an SPF record.</summary>
    /// <param name="record">Raw TXT record.</param>
    /// <param name="result">Parsed record.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    public static bool TryParse(string record, out SpfRecord? result) {
        result = null;
        if (string.IsNullOrWhiteSpace(record)) {
            return false;
        }
        var parts = record.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        var version = parts[0];
        string[] mechanisms;
        if (parts.Length > 1) {
            mechanisms = new string[parts.Length - 1];
            Array.Copy(parts, 1, mechanisms, 0, mechanisms.Length);
        } else {
            mechanisms = Array.Empty<string>();
        }
        result = new SpfRecord(version, mechanisms);
        return true;
    }
}
