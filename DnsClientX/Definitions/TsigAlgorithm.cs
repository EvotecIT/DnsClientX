namespace DnsClientX {
    /// <summary>
    /// HMAC algorithms supported for TSIG authentication.
    /// </summary>
    public enum TsigAlgorithm {
        /// <summary>HMAC-SHA-256, the mandatory-to-implement algorithm from RFC 8945.</summary>
        HmacSha256,
        /// <summary>HMAC-SHA-384.</summary>
        HmacSha384,
        /// <summary>HMAC-SHA-512.</summary>
        HmacSha512,
        /// <summary>HMAC-SHA-1 for compatibility with older authoritative servers.</summary>
        HmacSha1
    }
}
