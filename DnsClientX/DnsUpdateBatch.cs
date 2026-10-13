using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX;

/// <summary>A snapshot of operations and prerequisites sent as one atomic RFC 2136 UPDATE.</summary>
public sealed class DnsUpdateBatch {
    /// <summary>Creates a bounded atomic update; each catalog may contain at most 1024 entries.</summary>
    public DnsUpdateBatch(string zone, IEnumerable<DnsUpdateOperation> operations,
        IEnumerable<DnsUpdatePrerequisite>? prerequisites = null) {
        Zone = DnsWireNameCodec.Normalize(zone);
        if (operations == null) { throw new ArgumentNullException(nameof(operations)); }
        DnsUpdateOperation[] updates = operations.Take(1025).ToArray();
        DnsUpdatePrerequisite[] conditions = prerequisites?.Take(1025).ToArray() ?? Array.Empty<DnsUpdatePrerequisite>();
        if (updates.Length == 0 || updates.Length > 1024 || updates.Any(item => item == null)) {
            throw new ArgumentException("An update requires 1–1024 non-null operations.", nameof(operations));
        }
        if (conditions.Length > 1024 || conditions.Any(item => item == null)) {
            throw new ArgumentException("An update permits at most 1024 non-null prerequisites.", nameof(prerequisites));
        }
        Operations = Array.AsReadOnly(updates); Prerequisites = Array.AsReadOnly(conditions);
    }
    /// <summary>Gets the update zone.</summary>
    public string Zone { get; }
    /// <summary>Gets the immutable operation snapshot.</summary>
    public IReadOnlyList<DnsUpdateOperation> Operations { get; }
    /// <summary>Gets prerequisites evaluated before any mutation is applied.</summary>
    public IReadOnlyList<DnsUpdatePrerequisite> Prerequisites { get; }
}
