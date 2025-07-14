namespace DnsClientX;
/// <summary>
/// Enumerates DNS record types as defined in
/// <a href="https://www.rfc-editor.org/rfc/rfc1035#section-3.2.2">RFC 1035 section 3.2.2</a>.
/// See also the <a href="https://www.iana.org/assignments/dns-parameters/dns-parameters.xhtml#dns-parameters-4">IANA DNS Parameters</a> registry.
/// </summary>
/// <remarks>
/// These values are used when constructing queries via <see cref="ClientX"/> to
/// indicate the resource record type to request.
/// </remarks>
public enum DnsRecordType : ushort {
    /// <summary>
    /// Reserved record type.
    /// </summary>
    Reserved = 0,
    /// <summary>
    /// A host address.
    /// </summary>
    A = 1,
    /// <summary>
    /// An authoritative name server.
    /// </summary>
    NS = 2,
    /// <summary>
    /// A mail destination (obsolete, use MX).
    /// </summary>
    MD = 3,
    /// <summary>
    /// A mail forwarder (obsolete, use MX).
    /// </summary>
    MF = 4,
    /// <summary>
    /// The canonical name for an alias.
    /// </summary>
    CNAME = 5,
    /// <summary>
    /// Marks the start of a zone of authority.
    /// </summary>
    SOA = 6,
    /// <summary>
    /// A mailbox domain name (experimental).
    /// </summary>
    MB = 7,
    /// <summary>
    /// A mail group member (experimental).
    /// </summary>
    MG = 8,
    /// <summary>
    /// A mail rename domain name (experimental).
    /// </summary>
    MR = 9,
    /// <summary>
    /// A null record (experimental).
    /// </summary>
    NULL = 10,
    /// <summary>
    /// Well-known service description.
    /// </summary>
    WKS = 11,
    /// <summary>
    /// A domain name pointer.
    /// </summary>
    PTR = 12,
    /// <summary>
    /// Host information.
    /// </summary>
    HINFO = 13,
    /// <summary>
    /// Mailbox or mail list information.
    /// </summary>
    MINFO = 14,
    /// <summary>
    /// Mail exchange.
    /// </summary>
    MX = 15,
    /// <summary>
    /// Text strings.
    /// </summary>
    TXT = 16,
    /// <summary>
    /// Responsible person.
    /// </summary>
    RP = 17,
    /// <summary>
    /// AFS database location.
    /// </summary>
    AFSDB = 18,
    /// <summary>
    /// X.25 PSDN address.
    /// </summary>
    X25 = 19,
    /// <summary>
    /// ISDN address.
    /// </summary>
    ISDN = 20,
    /// <summary>
    /// Route through.
    /// </summary>
    RT = 21,
    /// <summary>
    /// NSAP address record (deprecated).
    /// </summary>
    NSAP = 22,
    /// <summary>
    /// Domain name pointer, NSAP style (deprecated).
    /// </summary>
    NSAP_PTR = 23,
    /// <summary>
    /// Security signature.
    /// </summary>
    SIG = 24,
    /// <summary>
    /// IPv6 address.
    /// </summary>
    AAAA = 28,
    /// <summary>
    /// Location information.
    /// </summary>
    LOC = 29,
    /// <summary>
    /// Server selection.
    /// </summary>
    SRV = 33,
    /// <summary>
    /// ATM address.
    /// </summary>
    ATMA = 34,
    /// <summary>
    /// Naming authority pointer.
    /// </summary>
    NAPTR = 35,
    /// <summary>
    /// Key exchanger.
    /// </summary>
    KX = 36,
    /// <summary>
    /// Certificate record.
    /// </summary>
    CERT = 37,
    /// <summary>
    /// A6 record (obsolete, use AAAA).
    /// </summary>
    A6 = 38,
    /// <summary>
    /// Non-terminal DNAME redirection.
    /// </summary>
    DNAME = 39,
    /// <summary>
    /// Kitchen sink (experimental).
    /// </summary>
    SINK = 40,
    /// <summary>
    /// Option pseudo-record.
    /// </summary>
    OPT = 41,
    /// <summary>
    /// Address prefix list.
    /// </summary>
    APL = 42,
    /// <summary>
    /// Delegation signer.
    /// </summary>
    DS = 43,
    /// <summary>
    /// SSH key fingerprint.
    /// </summary>
    SSHFP = 44,
    /// <summary>
    /// IPSECKEY record.
    /// </summary>
    IPSECKEY = 45,
    /// <summary>
    /// RRset signature.
    /// </summary>
    RRSIG = 46,
    /// <summary>
    /// Next secure record.
    /// </summary>
    NSEC = 47,
    /// <summary>
    /// DNS public key.
    /// </summary>
    DNSKEY = 48,
    /// <summary>
    /// DHCP identifier.
    /// </summary>
    DHCID = 49,
    /// <summary>
    /// NSEC3 record.
    /// </summary>
    NSEC3 = 50,
    /// <summary>
    /// NSEC3 parameters.
    /// </summary>
    NSEC3PARAM = 51,
    /// <summary>
    /// TLSA certificate association.
    /// </summary>
    TLSA = 52,
    /// <summary>
    /// S/MIME certificate association.
    /// </summary>
    SMIMEA = 53,
    /// <summary>
    /// Host identity protocol.
    /// </summary>
    HIP = 55,
    /// <summary>
    /// NINFO record.
    /// </summary>
    NINFO = 56,
    /// <summary>
    /// RKEY record.
    /// </summary>
    RKEY = 57,
    /// <summary>
    /// Trust anchor link.
    /// </summary>
    TALINK = 58,
    /// <summary>
    /// Child DS.
    /// </summary>
    CDS = 59,
    /// <summary>
    /// Child DNSKEY(s) to be published as DS.
    /// </summary>
    CDNSKEY = 60,
    /// <summary>
    /// OpenPGP key.
    /// </summary>
    OPENPGPKEY = 61,
    /// <summary>
    /// Child-to-parent synchronization.
    /// </summary>
    CSYNC = 62,
    /// <summary>
    /// Message digest over zone data.
    /// </summary>
    ZONEMD = 63,
    /// <summary>
    /// General-purpose service binding.
    /// </summary>
    SVCB = 64,
    /// <summary>
    /// Service binding compatible with HTTP.
    /// </summary>
    HTTPS = 65,
    /// <summary>
    /// Transaction key.
    /// </summary>
    TKEY = 249,
    /// <summary>
    /// Transaction signature.
    /// </summary>
    TSIG = 250,
    /// <summary>
    /// Incremental zone transfer.
    /// </summary>
    IXFR = 251,
    /// <summary>
    /// Authoritative zone transfer.
    /// </summary>
    AXFR = 252,
    /// <summary>
    /// Transfer mailbox records.
    /// </summary>
    MAILB = 253,
    /// <summary>
    /// Transfer mail agent records.
    /// </summary>
    MAILA = 254,
    /// <summary>
    /// Wildcard match for any record type.
    /// </summary>
    ANY = 255,
    /// <summary>
    /// Sender Policy Framework.
    /// </summary>
    SPF = 99,
    /// <summary>
    /// Uniform Resource Identifier.
    /// </summary>
    URI = 256,
    /// <summary>
    /// Certification Authority Restriction.
    /// </summary>
    CAA = 257,
    /// <summary>
    /// Application Visibility and Control.
    /// </summary>
    AVC = 258,
    /// <summary>
    /// Digital Object Architecture.
    /// </summary>
    DOA = 259,
    /// <summary>
    /// Automatic Multicast Tunneling Relay.
    /// </summary>
    AMTRELAY = 260,
    /// <summary>
    /// Resolver information as key/value pairs.
    /// </summary>
    RESINFO = 261,
    /// <summary>
    /// DNSSEC trust authorities.
    /// </summary>
    TA = 32768,
    /// <summary>
    /// DNSSEC Lookaside Validation (obsolete).
    /// </summary>
    DLV = 32769
}