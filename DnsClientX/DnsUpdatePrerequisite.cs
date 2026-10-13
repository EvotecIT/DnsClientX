using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX;

/// <summary>Identifies an RFC 2136 prerequisite evaluated atomically by the authoritative server.</summary>
public enum DnsUpdatePrerequisiteKind {
    /// <summary>The owner has at least one RRset.</summary>
    NameExists,
    /// <summary>The owner has no RRsets.</summary>
    NameAbsent,
    /// <summary>The RRset exists, regardless of its values.</summary>
    RrsetExists,
    /// <summary>The RRset does not exist.</summary>
    RrsetAbsent,
    /// <summary>The RRset contains exactly the supplied set of values.</summary>
    RrsetEquals
}

/// <summary>One immutable name or RRset prerequisite for an atomic UPDATE.</summary>
public sealed class DnsUpdatePrerequisite {
    private DnsUpdatePrerequisite(string name, DnsRecordType type, DnsUpdatePrerequisiteKind kind, IEnumerable<string>? values = null) {
        if (string.IsNullOrWhiteSpace(name)) { throw new ArgumentException("An owner name is required.", nameof(name)); }
        DnsUpdateMessage.ValidateRecordType(type);
        string[] snapshot = values?.Take(1025).ToArray() ?? Array.Empty<string>();
        if (snapshot.Length > 1024 || snapshot.Any(value => value == null)
            || kind == DnsUpdatePrerequisiteKind.RrsetEquals && snapshot.Length == 0) {
            throw new ArgumentException("An RRset comparison requires 1–1024 non-null values.", nameof(values));
        }
        Name = name; RecordType = type; Kind = kind; Values = Array.AsReadOnly(snapshot);
    }
    /// <summary>Gets the owner name.</summary>
    public string Name { get; }
    /// <summary>Gets the RRset type, or ANY for a name prerequisite.</summary>
    public DnsRecordType RecordType { get; }
    /// <summary>Gets the prerequisite kind.</summary>
    public DnsUpdatePrerequisiteKind Kind { get; }
    /// <summary>Gets the complete expected RRset for a value-dependent prerequisite.</summary>
    public IReadOnlyList<string> Values { get; }
    /// <summary>Requires at least one RRset at the owner.</summary>
    public static DnsUpdatePrerequisite NameExists(string name) => new(name, DnsRecordType.ANY, DnsUpdatePrerequisiteKind.NameExists);
    /// <summary>Requires no RRsets at the owner.</summary>
    public static DnsUpdatePrerequisite NameAbsent(string name) => new(name, DnsRecordType.ANY, DnsUpdatePrerequisiteKind.NameAbsent);
    /// <summary>Requires the named RRset, irrespective of its values.</summary>
    public static DnsUpdatePrerequisite RrsetExists(string name, DnsRecordType type) => new(name, type, DnsUpdatePrerequisiteKind.RrsetExists);
    /// <summary>Requires the named RRset to be absent.</summary>
    public static DnsUpdatePrerequisite RrsetAbsent(string name, DnsRecordType type) => new(name, type, DnsUpdatePrerequisiteKind.RrsetAbsent);
    /// <summary>Requires the RRset to equal the complete supplied set of values.</summary>
    public static DnsUpdatePrerequisite RrsetEquals(string name, DnsRecordType type, IEnumerable<string> values) =>
        new(name, type, DnsUpdatePrerequisiteKind.RrsetEquals, values ?? throw new ArgumentNullException(nameof(values)));
}
