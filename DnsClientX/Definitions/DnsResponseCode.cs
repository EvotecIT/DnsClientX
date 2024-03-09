namespace DnsClientX {
    /// <summary>
    /// Enumerates the possible status codes (DNS RCODEs) for a DNS query.
    /// For more information, see the <a href="https://www.iana.org/assignments/dns-parameters/dns-parameters.xhtml">IANA DNS Parameters</a> and the relevant RFCs: <a href="https://www.iana.org/go/rfc6895">RFC 6895</a> and <a href="https://www.iana.org/go/rfc1035">RFC 1035</a>.
    /// </summary>
    public enum DnsResponseCode : byte {
        /// <summary>
        /// The DNS query completed successfully.
        /// </summary>
        NoError,
        /// <summary>
        /// The DNS query was formatted incorrectly.
        /// </summary>
        FormatError,
        /// <summary>
        /// The server failed to complete the DNS query due to an internal error.
        /// </summary>
        ServerFailure,
        /// <summary>
        /// The queried domain name does not exist.
        /// </summary>
        NXDomain,
        /// <summary>
        /// The server does not support the requested operation.
        /// </summary>
        NotImplemented,
        /// <summary>
        /// The server refused to respond to the DNS query.
        /// </summary>
        Refused,
        /// <summary>
        /// A domain name that should not exist, does exist.
        /// </summary>
        YXDomain,
        /// <summary>
        /// A resource record set exists when it should not.
        /// </summary>
        YXRRSet,
        /// <summary>
        /// A resource record set that should exist, does not.
        /// </summary>
        NXRRSet,
        /// <summary>
        /// The server is not authoritative for the queried zone, or the server is not authorized to give a response.
        /// </summary>
        NotAuth,
        /// <summary>
        /// The queried name is not contained in the zone.
        /// </summary>
        NotZone,
        /// <summary>
        /// The DSO-TYPE is not implemented.
        /// </summary>
        DSOTYPENotImplemented,
        /// <summary>
        /// The OPT version is not supported.
        /// </summary>
        BadVersion = 16,
        /// <summary>
        /// The TSIG signature failed to validate.
        /// </summary>
        BadSignature,
        /// <summary>
        /// The key used for the query was not recognized.
        /// </summary>
        BadKey,
        /// <summary>
        /// The signature is outside of the valid time window.
        /// </summary>
        BadTime,
        /// <summary>
        /// The TKEY mode is not valid.
        /// </summary>
        BadMode,
        /// <summary>
        /// The key name is duplicated.
        /// </summary>
        BadName,
        /// <summary>
        /// The algorithm used for the query is not supported.
        /// </summary>
        BadAlgorithm,
        /// <summary>
        /// The message was truncated in a way that is not allowed.
        /// </summary>
        BadTruncation,
        /// <summary>
        /// The server cookie is either bad or missing.
        /// </summary>
        BadCookie
    }
}
