using System;

namespace DnsClientX {
    /// <summary>
    /// Represents a persisted probe or benchmark scoring snapshot.
    /// </summary>
    public sealed class ResolverScoreSnapshot {
        /// <summary>
        /// Gets or sets the snapshot schema version.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// Gets or sets the UTC time when the snapshot was generated.
        /// </summary>
        public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the run-level summary for the snapshot.
        /// </summary>
        public ResolverScoreSummary Summary { get; set; } = new ResolverScoreSummary();

        /// <summary>
        /// Gets or sets the scored candidate entries for the snapshot.
        /// </summary>
        public ResolverScoreEntry[] Results { get; set; } = Array.Empty<ResolverScoreEntry>();
    }
}
