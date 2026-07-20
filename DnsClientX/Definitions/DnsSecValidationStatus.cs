namespace DnsClientX {
    /// <summary>
    /// Describes the outcome of local DNSSEC validation.
    /// </summary>
    public enum DnsSecValidationStatus {
        /// <summary>DNSSEC validation was not requested.</summary>
        NotRequested,

        /// <summary>A complete chain from the answer to a configured trust anchor was verified.</summary>
        Secure,

        /// <summary>A secure parent proved that the answer's zone is unsigned.</summary>
        Insecure,

        /// <summary>Cryptographic data was present but failed validation.</summary>
        Bogus,

        /// <summary>The available data or supported algorithms were insufficient to prove a result.</summary>
        Indeterminate
    }
}
