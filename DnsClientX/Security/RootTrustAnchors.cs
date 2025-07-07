namespace DnsClientX {
    /// <summary>
    /// Represents a DNSSEC DS record used as a trust anchor.
    /// </summary>
    internal readonly struct RootDsRecord {
        /// <summary>Key tag of the DNSKEY record.</summary>
        public ushort KeyTag { get; }
        /// <summary>DNSKEY algorithm identifier.</summary>
        public DnsKeyAlgorithm Algorithm { get; }
        /// <summary>Digest type as defined by RFC 4034.</summary>
        public byte DigestType { get; }
        /// <summary>Hex-encoded digest value.</summary>
        public string Digest { get; }

        public RootDsRecord(ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest) {
            KeyTag = keyTag;
            Algorithm = algorithm;
            DigestType = digestType;
            Digest = digest;
        }
    }

    /// <summary>
    /// Collection of built-in root trust anchors for DNSSEC validation.
    /// </summary>
    internal static class RootTrustAnchors {
        /// <summary>Default DS records for the DNS root.</summary>
        internal static readonly RootDsRecord[] DsRecords = {
            new RootDsRecord(20326, DnsKeyAlgorithm.RSASHA256, 2, "E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D"),
            new RootDsRecord(38696, DnsKeyAlgorithm.RSASHA256, 2, "683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16")
        };
    }
}
