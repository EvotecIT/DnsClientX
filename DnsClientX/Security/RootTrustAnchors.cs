namespace DnsClientX {
    /// <summary>
    /// Represents a DNSSEC DS record used as a trust anchor.
    /// </summary>
    internal readonly struct RootDsRecord {
        public ushort KeyTag { get; }
        public DnsKeyAlgorithm Algorithm { get; }
        public byte DigestType { get; }
        public string Digest { get; }

        public RootDsRecord(ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest) {
            KeyTag = keyTag;
            Algorithm = algorithm;
            DigestType = digestType;
            Digest = digest;
        }
    }

    /// <summary>
    /// Provides built-in DNSSEC trust anchors for the root zone.
    /// </summary>
    internal static class RootTrustAnchors {
        internal static readonly RootDsRecord[] DsRecords = {
            new RootDsRecord(20326, DnsKeyAlgorithm.RSASHA256, 2, "E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D"),
            new RootDsRecord(38696, DnsKeyAlgorithm.RSASHA256, 2, "683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16")
        };
    }
}
