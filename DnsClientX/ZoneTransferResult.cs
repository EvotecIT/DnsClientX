using System;

namespace DnsClientX {
    /// <summary>
    /// Represents a chunk of records returned during a zone transfer.
    /// </summary>
    public readonly record struct ZoneTransferResult {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZoneTransferResult"/> struct.
        /// </summary>
        public ZoneTransferResult(DnsAnswer[] records, bool isOpening, bool isClosing, int index) {
            Records = records;
            IsOpening = isOpening;
            IsClosing = isClosing;
            Index = index;
        }

        /// <summary>Records contained in this chunk.</summary>
        public DnsAnswer[] Records { get; }

        /// <summary>True if the chunk contains the opening SOA record.</summary>
        public bool IsOpening { get; }

        /// <summary>True if the chunk contains the closing SOA record.</summary>
        public bool IsClosing { get; }

        /// <summary>Zero-based sequence number of this chunk.</summary>
        public int Index { get; }

        /// <summary>
        /// Gets the SOA record when <see cref="IsOpening"/> or <see cref="IsClosing"/> is true; otherwise, <c>null</c>.
        /// </summary>
        public DnsAnswer? SoaRecord => (IsOpening || IsClosing) && Records.Length > 0 ? Records[0] : null;
    }
}
