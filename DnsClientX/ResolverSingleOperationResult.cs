using System;

namespace DnsClientX {
    /// <summary>
    /// Represents the outcome of one shared single-target query or update operation.
    /// </summary>
    public sealed class ResolverSingleOperationResult {
        /// <summary>
        /// Gets or sets the DNS response produced by the operation.
        /// </summary>
        public DnsResponse Response { get; init; } = new DnsResponse();

        /// <summary>
        /// Gets or sets the total elapsed time for the operation.
        /// </summary>
        public TimeSpan Elapsed { get; init; }

        /// <summary>
        /// Gets or sets the configured selection strategy.
        /// </summary>
        public DnsSelectionStrategy SelectionStrategy { get; init; }

        /// <summary>
        /// Gets or sets the configured request format.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether response caching was enabled.
        /// </summary>
        public bool CacheEnabled { get; init; }

        /// <summary>
        /// Gets or sets the audit trail captured during the operation.
        /// </summary>
        public AuditEntry[] AuditTrail { get; init; } = Array.Empty<AuditEntry>();

        /// <summary>
        /// Gets or sets the configured resolver host when known.
        /// </summary>
        public string? ConfiguredResolverHost { get; init; }

        /// <summary>
        /// Gets or sets the configured resolver port.
        /// </summary>
        public int ConfiguredResolverPort { get; init; }
    }
}
