namespace DnsClientX;
using System;
using System.Linq;
using System.Net;
/// <summary>
/// Factory to convert <see cref="DnsAnswer"/> into typed record objects.
/// </summary>
public static class DnsRecordFactory {
    /// <summary>
    /// Parses an answer into a typed record if the type is known.
    /// </summary>
    /// <param name="answer">Answer to parse.</param>
    /// <param name="parseTypedTxtRecords">Whether to parse TXT records into specialized types (DMARC, SPF, etc.). When false, returns simple TXT records.</param>
    /// <returns>A typed record, or an <see cref="UnknownRecord"/> preserving unsupported or malformed data.</returns>
    public static object? Create(DnsAnswer answer, bool parseTypedTxtRecords = false) {
        string data = answer.Data;
        if (data.TrimStart().StartsWith("\\#", StringComparison.Ordinal)) return new UnknownRecord(data);
        switch (answer.Type) {
            case DnsRecordType.A:
                if (IPAddress.TryParse(data, out var ip4)) {
                    return new ARecord(ip4);
                }
                break;
            case DnsRecordType.AAAA:
                if (IPAddress.TryParse(data, out var ip6)) {
                    return new AAAARecord(ip6);
                }
                break;
            case DnsRecordType.CNAME:
                return new CNameRecord(DnsWireNameCodec.TrimTrailingRootDot(data));
            case DnsRecordType.MX:
                var parts = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out int pref)) {
                    return new MxRecord(pref, DnsWireNameCodec.TrimTrailingRootDot(parts[1]));
                }
                break;
            case DnsRecordType.NS:
                return new NsRecord(DnsWireNameCodec.TrimTrailingRootDot(data));
            case DnsRecordType.PTR:
                return new PtrRecord(DnsWireNameCodec.TrimTrailingRootDot(data));
            case DnsRecordType.TXT:
            case DnsRecordType.SPF:
                if (!parseTypedTxtRecords) {
                    return new TxtRecord(answer, data);
                }
                if (DmarcRecord.TryParse(data, out var dmarc)) {
                    return dmarc;
                }
                if (DkimRecord.TryParse(data, out var dkim)) {
                    return dkim;
                }
                if (SpfRecord.TryParse(data, out var spf)) {
                    return spf;
                }
                if (DomainVerificationRecord.TryParse(data, out var verify)) {
                    return verify;
                }
                if (KeyValueTxtRecord.TryParse(data, out var kv)) {
                    return kv;
                }
                return new TxtRecord(answer, data);
            case DnsRecordType.SOA:
                var soa = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (soa.Length == 7 &&
                    uint.TryParse(soa[2], out var serial) &&
                    uint.TryParse(soa[3], out var refresh) &&
                    uint.TryParse(soa[4], out var retry) &&
                    uint.TryParse(soa[5], out var expire) &&
                    uint.TryParse(soa[6], out var minimum)) {
                    return new SoaRecord(DnsWireNameCodec.TrimTrailingRootDot(soa[0]), DnsWireNameCodec.TrimTrailingRootDot(soa[1]), serial, refresh, retry, expire, minimum);
                }
                break;
            case DnsRecordType.SRV:
                var srv = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (srv.Length == 4 &&
                    ushort.TryParse(srv[0], out var prio) &&
                    ushort.TryParse(srv[1], out var weight) &&
                    ushort.TryParse(srv[2], out var port)) {
                    return new SrvRecord(prio, weight, port, DnsWireNameCodec.TrimTrailingRootDot(srv[3]));
                }
                break;
            case DnsRecordType.DNSKEY:
                var dnskey = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (dnskey.Length == 4 &&
                    ushort.TryParse(dnskey[0], out var flags) &&
                    byte.TryParse(dnskey[1], out var protocol) &&
                    Enum.TryParse<DnsKeyAlgorithm>(dnskey[2], true, out var alg)) {
                    return new DnsKeyRecord(flags, protocol, alg, dnskey[3]);
                }
                break;
            case DnsRecordType.DS:
                var ds = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (ds.Length == 4 &&
                    ushort.TryParse(ds[0], out var keyTag) &&
                    Enum.TryParse<DnsKeyAlgorithm>(ds[1], true, out var dsAlg) &&
                    byte.TryParse(ds[2], out var digestType)) {
                    return new DsRecord(keyTag, dsAlg, digestType, ds[3]);
                }
                break;
            case DnsRecordType.CAA:
                var caa = DnsPresentationFormat.Tokenize(data, out bool caaComplete);
                if (caaComplete && caa.Count == 3 && byte.TryParse(caa[0].Value, out var flag)) {
                    return new CaaRecord(flag, DnsPresentationFormat.Unescape(caa[1].Value), DnsPresentationFormat.Unescape(caa[2].Value));
                }
                break;
            case DnsRecordType.TLSA:
                var tlsa = data.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (tlsa.Length == 4 &&
                    byte.TryParse(tlsa[0], out var cu) &&
                    byte.TryParse(tlsa[1], out var selector) &&
                    byte.TryParse(tlsa[2], out var mt)) {
                    return new TlsaRecord(cu, selector, mt, tlsa[3]);
                }
                break;
            case DnsRecordType.NAPTR:
                var naptr = DnsPresentationFormat.Tokenize(data, out bool complete);
                if (complete && naptr.Count == 6 &&
                    ushort.TryParse(naptr[0].Value, out var order) &&
                    ushort.TryParse(naptr[1].Value, out var preference)) {
                    string replacement = naptr[5].Raw;
                    return new NaptrRecord(order, preference,
                        DnsPresentationFormat.Unescape(naptr[2].Value),
                        DnsPresentationFormat.Unescape(naptr[3].Value),
                        DnsPresentationFormat.Unescape(naptr[4].Value),
                        DnsWireNameCodec.TrimTrailingRootDot(replacement));
                }
                break;
            case DnsRecordType.DNAME:
                return new DnameRecord(DnsWireNameCodec.TrimTrailingRootDot(data));
            case DnsRecordType.LOC:
                if (DnsLocPresentation.TryParse(data, out var location)) return location;
                break;
            case DnsRecordType.SVCB:
            case DnsRecordType.HTTPS:
                if (DnsSvcbCodec.TryParse(data, out var service)) return service;
                break;
            default:
                return new UnknownRecord(data);
        }
        return new UnknownRecord(data);
    }
}
