using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DnsClientX {
    /// <summary>Identifies the response form returned for an RFC 1995 IXFR request.</summary>
    public enum IncrementalZoneTransferKind {
        /// <summary>The primary returned one SOA, indicating that no update is required.</summary>
        NoChange,

        /// <summary>The primary returned one or more complete delete/add difference sequences.</summary>
        Incremental,

        /// <summary>The primary could not provide differences and returned a complete AXFR-style zone.</summary>
        FullTransfer
    }

    /// <summary>Represents one atomic old-SOA/delete/new-SOA/add IXFR sequence.</summary>
    public sealed class IncrementalZoneChange {
        internal IncrementalZoneChange(SoaRecord previousSoa, SoaRecord currentSoa,
            IEnumerable<DnsAnswer> deletedRecords, IEnumerable<DnsAnswer> addedRecords) {
            PreviousSoa = previousSoa;
            CurrentSoa = currentSoa;
            DeletedRecords = Array.AsReadOnly(deletedRecords.ToArray());
            AddedRecords = Array.AsReadOnly(addedRecords.ToArray());
        }

        /// <summary>Gets the SOA version to which deletions apply.</summary>
        public SoaRecord PreviousSoa { get; }

        /// <summary>Gets the SOA version produced by this change.</summary>
        public SoaRecord CurrentSoa { get; }

        /// <summary>Gets the records deleted by this change, excluding the delimiting SOA.</summary>
        public IReadOnlyList<DnsAnswer> DeletedRecords { get; }

        /// <summary>Gets the records added by this change, excluding the delimiting SOA.</summary>
        public IReadOnlyList<DnsAnswer> AddedRecords { get; }
    }

    /// <summary>Represents a complete, validated IXFR response.</summary>
    public sealed class IncrementalZoneTransferResult {
        internal IncrementalZoneTransferResult(IncrementalZoneTransferKind kind, SoaRecord currentSoa,
            IEnumerable<IncrementalZoneChange>? changes = null, IEnumerable<DnsAnswer>? fullZoneRecords = null) {
            Kind = kind;
            CurrentSoa = currentSoa;
            Changes = Array.AsReadOnly((changes ?? Array.Empty<IncrementalZoneChange>()).ToArray());
            FullZoneRecords = Array.AsReadOnly((fullZoneRecords ?? Array.Empty<DnsAnswer>()).ToArray());
        }

        /// <summary>Gets whether the response was no-change, incremental, or a full-transfer fallback.</summary>
        public IncrementalZoneTransferKind Kind { get; }

        /// <summary>Gets the primary's current SOA after the transfer.</summary>
        public SoaRecord CurrentSoa { get; }

        /// <summary>Gets complete validated changes. They should be applied atomically in order.</summary>
        public IReadOnlyList<IncrementalZoneChange> Changes { get; }

        /// <summary>Gets complete AXFR-style records when <see cref="Kind"/> is <see cref="IncrementalZoneTransferKind.FullTransfer"/>.</summary>
        public IReadOnlyList<DnsAnswer> FullZoneRecords { get; }
    }
}
