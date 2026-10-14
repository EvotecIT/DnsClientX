namespace DnsClientX {
    /// <summary>Correlates an observed DNS status and answer set with its resolver and query.</summary>
    public sealed class ResolverAnswerObservation {
        /// <summary>Gets or sets the resolver target.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Gets or sets the stable configured candidate identity; empty for legacy label-only observations.</summary>
        public string CandidateId { get; set; } = string.Empty;
        /// <summary>Gets or sets the query name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the query type.</summary>
        public DnsRecordType RecordType { get; set; }
        /// <summary>Gets or sets the observed status, including NoResponse.</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Gets or sets the TTL-independent normalized answer signature.</summary>
        public string AnswerSignature { get; set; } = string.Empty;
    }
    /// <summary>Provides a caller-supplied authoritative response for one query; its origin is not authenticated by comparison.</summary>
    public sealed class ResolverAnswerReference {
        /// <summary>Gets or sets the query name associated with the reference.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets the reference query type.</summary>
        public DnsRecordType RecordType { get; set; }
        /// <summary>Gets or sets the caller's source description.</summary>
        public string Source { get; set; } = string.Empty;
        /// <summary>Gets or sets the reference response. Its AA flag must be set.</summary>
        public DnsResponse Response { get; set; } = new DnsResponse();
    }
}
