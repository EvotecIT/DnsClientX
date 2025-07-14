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
        /// Gets or sets the response returned by the server.
        /// </summary>
        public DnsResponse? Response { get; set; }

        /// <summary>
        /// Gets or sets the exception thrown during resolution if any.
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
