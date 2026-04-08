using System;

namespace DnsClientX {
    /// <summary>
    /// Represents the outcome of a recursive authoritative zone transfer attempt.
    /// </summary>
    public sealed class RecursiveZoneTransferResult {
        /// <summary>
        /// Gets or sets the requested zone.
        /// </summary>
        public string Zone { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authoritative NS hostname associated with the successful transfer.
        /// </summary>
        public string SelectedAuthority { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the concrete host or address used for the successful transfer.
        /// </summary>
        public string SelectedServer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the TCP port used for AXFR.
        /// </summary>
        public int Port { get; set; } = 53;

        /// <summary>
        /// Gets or sets the authoritative NS hostnames discovered for the zone.
        /// </summary>
        public string[] Authorities { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the ordered transfer targets that were attempted.
        /// </summary>
        public string[] TriedServers { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the transferred RRsets from the successful AXFR operation.
        /// </summary>
        public ZoneTransferResult[] RecordSets { get; set; } = Array.Empty<ZoneTransferResult>();
    }
}
