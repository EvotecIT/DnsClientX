namespace DnsClientX;
/// <summary>
/// Type of DNS record as defined in RFC 1035.
/// c.f. https://www.iana.org/assignments/dns-parameters/dns-parameters.xhtml#dns-parameters-4
/// </summary>
public enum DnsRecordType : ushort {
    /// <summary>
    /// The reserved record type.
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
    /// A mail destination (OBSOLETE - use MX).
    /// </summary>
    MD = 3,
    /// <summary>
    /// A mail forwarder (OBSOLETE - use MX).
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
    /// A mailbox domain name (EXPERIMENTAL).
    /// </summary>
    MB = 7,
    /// <summary>
    /// A mail group member (EXPERIMENTAL).
    /// </summary>
    MG = 8,
    /// <summary>
    /// A mail rename domain name (EXPERIMENTAL).
    /// </summary>
    MR = 9,
    /// <summary>
    /// A null RR (EXPERIMENTAL).
    /// </summary>
    NULL = 10,
    /// <summary>
    /// A well known service description.
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
    /// For responsible person.
    /// </summary>
    RP = 17,
    /// <summary>
    /// AFS Data Base location.
    /// </summary>
    AFSDB = 18,
    /// <summary>
    /// for X.25 PSDN address
    /// </summary>
    X25 = 19,
    /// <summary>
    /// ISDN address.
    /// </summary>
    ISDN = 20,
    /// <summary>
    /// for Route Through.
    /// </summary>
    RT = 21,
    /// <summary>
    /// for NSAP address, NSAP style A record (DEPRECATED)
    /// </summary>
    NSAP = 22,
    /// <summary>
    /// for domain name pointer, NSAP style (DEPRECATED)
    /// </summary>
    NSAP_PTR = 23,
    /// <summary>
    /// For a security signature.
    /// </summary>
    SIG = 24,
    /// <summary>
    /// IPv6 Address.
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
    /// ATM Address
    /// </summary>
    ATMA = 34,
    /// <summary>
    /// Naming Authority Pointer.
    /// </summary>
    NAPTR = 35,
    /// <summary>
    /// Key Exchanger.
    /// </summary>
    KX = 36,
    /// <summary>
    /// CERT
    /// </summary>
    CERT = 37,
    /// <summary>
    /// A6 (OBSOLETE - use AAAA).
    /// </summary>
    A6 = 38,
    /// <summary>
    /// DNAME Record.
    /// </summary>
    DNAME = 39,
    /// <summary>
    /// The sink
    /// </summary>
    SINK = 40,
    /// <summary>
    /// The opt
    /// </summary>
    OPT = 41,
    /// <summary>
    /// The apl
    /// </summary>
    APL = 42,
    /// <summary>
    /// The ds
    /// </summary>
    DS = 43,
    /// <summary>
    /// The SSHFP
    /// </summary>
    SSHFP = 44,
    /// <summary>
    /// IPSECKEY Record.
    /// </summary>
    IPSECKEY = 45,
    /// <summary>
    /// RRset Signature.
    /// </summary>
    RRSIG = 46,
    /// <summary>
    /// NSEC
    /// </summary>
    NSEC = 47,
    /// <summary>
    /// DNSKEY Record.
    /// </summary>
    DNSKEY = 48,
    /// <summary>
    /// The dhcid
    /// </summary>
    DHCID = 49,
    /// <summary>
    /// The nse c3
    /// </summary>
    NSEC3 = 50,
    /// <summary>
    /// The nse c3 parameter
    /// </summary>
    NSEC3PARAM = 51,
    /// <summary>
    /// The tlsa
    /// </summary>
    TLSA = 52,
    /// <summary>
    /// The smimea
    /// </summary>
    SMIMEA = 53,
    /// <summary>
    /// The hip
    /// </summary>
    HIP = 55,
    /// <summary>
    /// The ninfo
    /// </summary>
    NINFO = 56,
    /// <summary>
    /// The rkey
    /// </summary>
    RKEY = 57,
    /// <summary>
    /// Trust Anchor LINK.
    /// </summary>
    TALINK = 58,
    /// <summary>
    /// Child DS.
    /// </summary>
    CDS = 59,
    /// <summary>
    /// DNSKEY(s) the Child wants reflected in DS.
    /// </summary>
    CDNSKEY = 60,
    /// <summary>
    /// OpenPGP public key record.
    /// </summary>
    OPENPGPKEY = 61,
    /// <summary>
    /// Child to Parent Synchronization.
    /// </summary>
    CSYNC = 62,
    /// <summary>
    /// Message Digest for DNS Zones.
    /// </summary>
    ZONEMD = 63,
    /// <summary>
    /// Service Binding.
    /// </summary>
    SVCB = 64,
    /// <summary>
    /// Service Binding compatible type for use with HTTP
    /// </summary>
    HTTPS = 65,
    /// <summary>
    /// For the sender policy framework.
    /// </summary>
    SPF = 99,
    /// <summary>
    /// URI
    /// </summary>
    URI = 256,
    /// <summary>
    /// Certification Authority Authorization.
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
    /// Resolver information as KEY/VALUE pairs.
    /// </summary>
    RESINFO = 261,
    /// <summary>
    /// DNSSEC Trust Authorities.
    /// </summary>
    TA = 32768,
    /// <summary>
    /// DNSSEC Lookaside Validation. (OBSOLETE)
    /// </summary>
    DLV = 32769
}