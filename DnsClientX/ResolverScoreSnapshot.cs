using System;

namespace DnsClientX {
    /// <summary>
    /// Represents a persisted probe or benchmark scoring snapshot.
    /// </summary>
    public sealed class ResolverScoreSnapshot {
        /// <summary>
        /// The oldest resolver score snapshot schema version supported by this library.
        /// </summary>
        public const int MinimumSupportedSchemaVersion = 1;

        /// <summary>
        /// The resolver score snapshot schema version written by this library.
        /// </summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// Gets or sets the snapshot schema version.
        /// </summary>
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

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

        /// <summary>
        /// Validates whether a resolver score snapshot can be read by this library.
        /// </summary>
        /// <param name="snapshot">The snapshot to validate.</param>
        /// <param name="error">The compatibility error when validation fails.</param>
        /// <returns><c>true</c> when the snapshot schema is supported; otherwise <c>false</c>.</returns>
        public static bool TryValidateCompatibility(ResolverScoreSnapshot? snapshot, out string? error) {
            error = null;

            if (snapshot == null) {
                error = "Resolver score snapshot is required.";
                return false;
            }

            if (snapshot.SchemaVersion < MinimumSupportedSchemaVersion) {
                error = $"Resolver score snapshot schema version {snapshot.SchemaVersion} is not supported. Supported schema versions are {MinimumSupportedSchemaVersion} through {CurrentSchemaVersion}.";
                return false;
            }

            if (snapshot.SchemaVersion > CurrentSchemaVersion) {
                error = $"Resolver score snapshot schema version {snapshot.SchemaVersion} was produced by a newer DnsClientX release. This release supports schema versions {MinimumSupportedSchemaVersion} through {CurrentSchemaVersion}.";
                return false;
            }

            return true;
        }
    }
}
