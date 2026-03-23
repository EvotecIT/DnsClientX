using System;

namespace DnsClientX {
    /// <summary>
    /// Represents a single audit log entry containing query details,
    /// the resulting response and any exception that occurred.
    /// </summary>
    /// <remarks>
    /// The audit trail collected by <see cref="ClientX"/> exposes these entries to callers for troubleshooting.
    /// </remarks>
    public class AuditEntry {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuditEntry"/> class.
        /// </summary>
        /// <param name="name">The queried domain name.</param>
        /// <param name="recordType">The requested record type.</param>
        public AuditEntry(string name, DnsRecordType recordType) {
            Name = name;
            RecordType = recordType;
        }

        /// <summary>
        /// Gets the queried domain name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the requested record type.
        /// </summary>
        public DnsRecordType RecordType { get; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the attempt started.
        /// </summary>
        public DateTimeOffset StartedAtUtc { get; internal set; }

        /// <summary>
        /// Gets or sets the duration of the attempt.
        /// </summary>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// Gets or sets the selection strategy used for the attempt.
        /// </summary>
        public DnsSelectionStrategy SelectionStrategy { get; internal set; }

        /// <summary>
        /// Gets or sets the resolver host selected for the attempt.
        /// </summary>
        public string? ResolverHost { get; internal set; }

        /// <summary>
        /// Gets or sets the resolver port used for the attempt.
        /// </summary>
        public int ResolverPort { get; internal set; }

        /// <summary>
        /// Gets or sets the request format used for the attempt.
        /// </summary>
        public DnsRequestFormat RequestFormat { get; internal set; }

        /// <summary>
        /// Gets or sets the actual transport used for the attempt.
        /// </summary>
        public Transport UsedTransport { get; internal set; }

        /// <summary>
        /// Gets or sets a value indicating whether the response came from cache.
        /// </summary>
        public bool ServedFromCache { get; internal set; }

        /// <summary>
        /// Gets or sets the attempt number within the lifetime of the client audit trail.
        /// </summary>
        public int AttemptNumber { get; internal set; }

        /// <summary>
        /// Gets or sets the reason a retry was triggered after this attempt.
        /// </summary>
        public string? RetryReason { get; internal set; }

        /// <summary>
        /// Gets or sets the response returned by the server.
        /// </summary>
        public DnsResponse? Response { get; set; }

        /// <summary>
        /// Gets or sets the exception thrown during resolution if any.
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
