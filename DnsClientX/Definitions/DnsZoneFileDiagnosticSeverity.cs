namespace DnsClientX {
    /// <summary>Identifies the severity of a zone-file parser diagnostic.</summary>
    public enum DnsZoneFileDiagnosticSeverity {
        /// <summary>The input was accepted with an informational note.</summary>
        Information,
        /// <summary>The parser recovered, but part of the input may not mean what the author intended.</summary>
        Warning,
        /// <summary>The affected directive or record could not be parsed.</summary>
        Error
    }
}
