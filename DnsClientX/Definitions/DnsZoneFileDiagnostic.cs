namespace DnsClientX {
    /// <summary>Describes a recoverable or fatal zone-file parsing problem.</summary>
    public sealed class DnsZoneFileDiagnostic {
        internal DnsZoneFileDiagnostic(string source, int line, DnsZoneFileDiagnosticSeverity severity, string message) {
            Source = source;
            Line = line;
            Severity = severity;
            Message = message;
        }

        /// <summary>Gets the source file or logical source name.</summary>
        public string Source { get; }
        /// <summary>Gets the one-based source line.</summary>
        public int Line { get; }
        /// <summary>Gets the diagnostic severity.</summary>
        public DnsZoneFileDiagnosticSeverity Severity { get; }
        /// <summary>Gets the diagnostic message.</summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Source}({Line}): {Severity}: {Message}";
    }
}
