namespace DnsClientX {
    /// <summary>
    /// Enumerates the DNS RCODE values returned in a response.
    /// See <a href="https://www.rfc-editor.org/rfc/rfc1035#section-4.1.1">RFC 1035 section 4.1.1</a>
    /// and <a href="https://www.rfc-editor.org/rfc/rfc6895">RFC 6895</a> for details.
    /// </summary>
    /// <remarks>
    /// This enumeration maps the numeric values returned in the DNS header's
    /// <c>RCODE</c> field to friendly names.
    /// </remarks>
    public enum DnsResponseCode : byte {
        /// <summary>
        /// The DNS query completed successfully.
        /// </summary>
        NoError = 0,
        /// <summary>
        /// The DNS query was formatted incorrectly.
        /// </summary>
        FormatError = 1,
        /// <summary>
        /// The server failed to complete the DNS query due to an internal error.
        /// </summary>
        ServerFailure = 2,
        /// <summary>
        /// The queried domain name does not exist.
        /// </summary>
        NXDomain = 3,
        /// <summary>
        /// The server does not support the requested operation.
        /// </summary>
        NotImplemented = 4,
        /// <summary>
        /// The server refused to respond to the DNS query.
        /// </summary>
        Refused = 5,
        /// <summary>
        /// A domain name that should not exist, does exist.
        /// </summary>
        YXDomain = 6,
        /// <summary>
        /// A resource record set exists when it should not.
        /// </summary>
        YXRRSet = 7,
        /// <summary>
        /// A resource record set that should exist, does not.
        /// </summary>
        NXRRSet = 8,
        /// <summary>
        /// The server is not authoritative for the queried zone, or the server is not authorized to give a response.
        /// </summary>
        NotAuth = 9,
        /// <summary>
        /// The queried name is not contained in the zone.
        /// </summary>
        NotZone = 10,

        // 11-15 are reserved for future use

        /// <summary>
        /// The DSO-TYPE is not implemented.
        /// </summary>
        DSOTYPENotImplemented = 11,

        // 12-15 are unassigned

        /// <summary>
        /// The OPT version is not supported.
        /// </summary>
        BadVersion = 16,
        /// <summary>
        /// The TSIG signature failed to validate.
        /// </summary>
        BadSignature = 17,
        /// <summary>
        /// The key used for the query was not recognized.
        /// </summary>
        BadKey = 18,
        /// <summary>
        /// The signature is outside the valid time window.
        /// </summary>
        BadTime = 19,
        /// <summary>
        /// The TKEY mode is not valid.
        /// </summary>
        BadMode = 20,
        /// <summary>
        /// The key name is duplicated.
        /// </summary>
        BadName = 21,
        /// <summary>
        /// The algorithm used for the query is not supported.
        /// </summary>
        BadAlgorithm = 22,
        /// <summary>
        /// The message was truncated in a way that is not allowed.
        /// </summary>
        BadTruncation = 23,
        /// <summary>
        /// The server cookie is either bad or missing.
        /// </summary>
        BadCookie = 24

        // 25-3840 are unassigned
        // 3841-4095 are reserved for Private Use
        // 4096-65534 are unassigned
        // 65535 is reserved
    }
}
