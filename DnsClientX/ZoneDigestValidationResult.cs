namespace DnsClientX {
    /// <summary>Describes the outcome of RFC 8976 ZONEMD validation.</summary>
    public enum ZoneDigestValidationStatus {
        /// <summary>At least one supported apex ZONEMD record matched the transferred zone.</summary>
        Valid,
        /// <summary>The zone did not contain an apex ZONEMD record.</summary>
        Missing,
        /// <summary>The zone contained ZONEMD records, but none used a supported scheme and hash.</summary>
        Unsupported,
        /// <summary>The zone digest or its surrounding zone metadata was malformed or did not match.</summary>
        Invalid
    }

    /// <summary>Represents an RFC 8976 zone-digest validation result.</summary>
    public sealed class ZoneDigestValidationResult {
        internal ZoneDigestValidationResult(ZoneDigestValidationStatus status, string message,
            uint? serial = null, byte? scheme = null, byte? hashAlgorithm = null) {
            Status = status;
            Message = message;
            Serial = serial;
            Scheme = scheme;
            HashAlgorithm = hashAlgorithm;
        }

        /// <summary>Gets the validation status.</summary>
        public ZoneDigestValidationStatus Status { get; }
        /// <summary>Gets a precise explanation of the result.</summary>
        public string Message { get; }
        /// <summary>Gets the validated SOA/ZONEMD serial when available.</summary>
        public uint? Serial { get; }
        /// <summary>Gets the matching ZONEMD scheme when validation succeeded.</summary>
        public byte? Scheme { get; }
        /// <summary>Gets the matching ZONEMD hash algorithm when validation succeeded.</summary>
        public byte? HashAlgorithm { get; }
        /// <summary>Gets whether the transferred bytes matched a supported ZONEMD record.</summary>
        public bool IsValid => Status == ZoneDigestValidationStatus.Valid;
        /// <summary>
        /// Gets whether this checksum result alone authenticates the zone origin. This is always false;
        /// authenticate the ZONEMD RRset with DNSSEC or another trusted channel separately.
        /// </summary>
        public bool ProvidesOriginAuthentication => false;
    }
}
