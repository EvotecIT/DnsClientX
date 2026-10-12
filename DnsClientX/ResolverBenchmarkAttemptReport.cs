namespace DnsClientX {
    /// <summary>Contains correlated, JSON-safe details for an individual benchmark query.</summary>
    public sealed class ResolverBenchmarkAttemptReport {
        /// <summary>Gets or sets the resolver target.</summary>
        public string Target { get; set; } = string.Empty;
        /// <summary>Gets or sets the queried name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the query type.</summary>
        public DnsRecordType? RecordType { get; set; }
        /// <summary>Gets or sets the response status or NoResponse.</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Gets or sets the effective error, including failures without a response.</summary>
        public string Error { get; set; } = string.Empty;
        /// <summary>Gets or sets the complete duration in milliseconds.</summary>
        public double ElapsedMs { get; set; }
        /// <summary>Gets or sets the primary transport duration, when measured.</summary>
        public double? TransportElapsedMs { get; set; }
        /// <summary>Gets or sets captured response evidence.</summary>
        public DnsQueryExplanation? Explanation { get; set; }
    }
}
