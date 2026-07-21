using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DnsClientX {
    /// <summary>Contains records and structured diagnostics produced by a zone-file parse.</summary>
    public sealed class DnsZoneFileParseResult {
        internal DnsZoneFileParseResult(IEnumerable<DnsAnswer> records, IEnumerable<DnsZoneFileDiagnostic> diagnostics) {
            Records = Array.AsReadOnly(records.ToArray());
            Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        }

        /// <summary>Gets all successfully parsed records.</summary>
        public ReadOnlyCollection<DnsAnswer> Records { get; }
        /// <summary>Gets parser diagnostics in source order.</summary>
        public ReadOnlyCollection<DnsZoneFileDiagnostic> Diagnostics { get; }
        /// <summary>Gets whether parsing completed without error diagnostics.</summary>
        public bool Success => !Diagnostics.Any(item => item.Severity == DnsZoneFileDiagnosticSeverity.Error);
    }
}
