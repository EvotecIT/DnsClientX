namespace DnsClientX;
using System;
using System.Globalization;
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
    /// <returns>Typed record instance or <c>null</c> if the type is not supported.</returns>
    public static object? Create(DnsAnswer answer) {
        switch (answer.Type) {
            case DnsRecordType.A:
                if (IPAddress.TryParse(answer.Data, out var ip4)) {
                    return new ARecord(ip4);
                }
                break;
            case DnsRecordType.AAAA:
                if (IPAddress.TryParse(answer.Data, out var ip6)) {
                    return new AAAARecord(ip6);
                }
                break;
            case DnsRecordType.CNAME:
                return new CNameRecord(answer.Data.TrimEnd('.'));
            case DnsRecordType.MX:
                var parts = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out int pref)) {
                    return new MxRecord(pref, parts[1].TrimEnd('.'));
                }
                break;
            case DnsRecordType.NS:
                return new NsRecord(answer.Data.TrimEnd('.'));
            case DnsRecordType.PTR:
                return new PtrRecord(answer.Data.TrimEnd('.'));
            case DnsRecordType.TXT:
                return new TxtRecord(answer.DataStrings);
            case DnsRecordType.SOA:
                var soa = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (soa.Length == 7 &&
                    uint.TryParse(soa[2], out var serial) &&
                    uint.TryParse(soa[3], out var refresh) &&
                    uint.TryParse(soa[4], out var retry) &&
                    uint.TryParse(soa[5], out var expire) &&
                    uint.TryParse(soa[6], out var minimum)) {
                    return new SoaRecord(soa[0].TrimEnd('.'), soa[1].TrimEnd('.'), serial, refresh, retry, expire, minimum);
                }
                break;
            case DnsRecordType.SRV:
                var srv = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (srv.Length == 4 &&
                    ushort.TryParse(srv[0], out var prio) &&
                    ushort.TryParse(srv[1], out var weight) &&
                    ushort.TryParse(srv[2], out var port)) {
                    return new SrvRecord(prio, weight, port, srv[3].TrimEnd('.'));
                }
                break;
            case DnsRecordType.DNSKEY:
                var dnskey = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (dnskey.Length >= 4 &&
                    ushort.TryParse(dnskey[0], out var flags) &&
                    byte.TryParse(dnskey[1], out var protocol) &&
                    Enum.TryParse<DnsKeyAlgorithm>(dnskey[2], true, out var alg)) {
                    return new DnsKeyRecord(flags, protocol, alg, dnskey[3]);
                }
                break;
            case DnsRecordType.DS:
                var ds = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (ds.Length >= 4 &&
                    ushort.TryParse(ds[0], out var keyTag) &&
                    Enum.TryParse<DnsKeyAlgorithm>(ds[1], true, out var dsAlg) &&
                    byte.TryParse(ds[2], out var digestType)) {
                    return new DsRecord(keyTag, dsAlg, digestType, ds[3]);
                }
                break;
            case DnsRecordType.CAA:
                var caa = answer.Data.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (caa.Length == 3 && byte.TryParse(caa[0], out var flag)) {
                    return new CaaRecord(flag, caa[1], caa[2].Trim('"'));
                }
                break;
            case DnsRecordType.TLSA:
                var tlsa = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tlsa.Length >= 4 &&
                    byte.TryParse(tlsa[0], out var cu) &&
                    byte.TryParse(tlsa[1], out var selector) &&
                    byte.TryParse(tlsa[2], out var mt)) {
                    return new TlsaRecord(cu, selector, mt, tlsa[3]);
                }
                break;
            case DnsRecordType.NAPTR:
                var naptr = answer.Data.Split(' ', 6, StringSplitOptions.RemoveEmptyEntries);
                if (naptr.Length >= 6 &&
                    ushort.TryParse(naptr[0], out var order) &&
                    ushort.TryParse(naptr[1], out var preference)) {
                    return new NaptrRecord(order, preference, naptr[2].Trim('"'), naptr[3].Trim('"'), naptr[4].Trim('"'), naptr[5].TrimEnd('.'));
                }
                break;
            case DnsRecordType.DNAME:
                return new DnameRecord(answer.Data.TrimEnd('.'));
            case DnsRecordType.LOC:
                var loc = answer.Data.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (loc.Length >= 11 &&
                    int.TryParse(loc[0], out var latDeg) &&
                    int.TryParse(loc[1], out var latMin) &&
                    double.TryParse(loc[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var latSec) &&
                    int.TryParse(loc[4], out var lonDeg) &&
                    int.TryParse(loc[5], out var lonMin) &&
                    double.TryParse(loc[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var lonSec) &&
                    double.TryParse(loc[8].TrimEnd('m'), NumberStyles.Float, CultureInfo.InvariantCulture, out var alt) &&
                    double.TryParse(loc[9].TrimEnd('m'), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) &&
                    double.TryParse(loc[10].TrimEnd('m'), NumberStyles.Float, CultureInfo.InvariantCulture, out var hp) &&
                    double.TryParse(loc[11].TrimEnd('m'), NumberStyles.Float, CultureInfo.InvariantCulture, out var vp)) {
                    double latitude = latDeg + latMin / 60d + latSec / 3600d;
                    if (loc[3] == "S") latitude = -latitude;
                    double longitude = lonDeg + lonMin / 60d + lonSec / 3600d;
                    if (loc[7] == "W") longitude = -longitude;
                    return new LocRecord(latitude, longitude, alt, size, hp, vp);
                }
                break;
            default:
                return new UnknownRecord(answer.Data);
        }
        return new UnknownRecord(answer.Data);
    }
}
