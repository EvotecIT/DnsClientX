using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace DnsClientX;
/// <summary>Describes one usable encrypted DNS transport advertised by a ServiceMode SVCB record.</summary>
/// <remarks>Selection describes protocol compatibility; it does not authenticate the advertised resolver.</remarks>
public sealed class DnsServiceBinding {
    /// <summary>Gets the source immutable service-binding record.</summary>
    public SvcbRecord Record { get; }
    /// <summary>Gets the resolved SVCB target name.</summary>
    public string Target { get; }
    /// <summary>Gets the selected ALPN identifier.</summary>
    public string Alpn { get; }
    /// <summary>Gets the corresponding encrypted request format.</summary>
    public DnsRequestFormat RequestFormat { get; }
    /// <summary>Gets the advertised port, or the selected transport's default port.</summary>
    public int Port { get; }
    /// <summary>Gets the validated relative DoH path template, or null for other transports.</summary>
    public DnsDohUriTemplate? DohPath { get; }
    /// <summary>Gets advertised IP hints, which are connection candidates rather than authenticated identities.</summary>
    public IReadOnlyList<IPAddress> AddressHints { get; }

    internal DnsServiceBinding(SvcbRecord record, string target, string alpn, DnsRequestFormat format, DnsDohUriTemplate? path) {
        Record = record;
        Target = target;
        Alpn = alpn;
        RequestFormat = format;
        Port = record.Port ?? (format is DnsRequestFormat.DnsOverTLS or DnsRequestFormat.DnsOverQuic ? 853 : 443);
        DohPath = path;
        AddressHints = Array.AsReadOnly(record.Ipv4Hints.Concat(record.Ipv6Hints).ToArray());
    }
}

/// <summary>Selects RFC 9461 encrypted DNS bindings without changing the client's current resolver.</summary>
public static class DnsServiceBindingSelection {
    private static readonly ushort[] UnderstoodKeys = {
        0,
        1,
        2,
        3,
        4,
        6,
        7
    };
    private static readonly string[] DefaultAlpn = {
        "h2",
        "http/1.1",
        "dot",
        "h3",
        "doq"
    };
    /// <summary>Returns compatible ServiceMode records by priority and caller ALPN preference.</summary>
    /// <param name = "ownerName">The SVCB owner name, used for a root-label ServiceMode target.</param>
    /// <param name = "records">The complete SVCB RRset. An AliasMode RRset requires another query and yields no bindings.</param>
    /// <param name = "supportedAlpn">Ordered ALPN identifiers; null supports the encrypted protocols recognized by this library.</param>
    /// <returns>Compatible bindings. Unknown mandatory keys, missing ALPN, and malformed DoH paths are incompatible.</returns>
    public static DnsServiceBinding[] SelectDnsResolvers(string ownerName, IEnumerable<SvcbRecord> records, IEnumerable<string>? supportedAlpn = null) {
        if (records == null) {
            throw new ArgumentNullException(nameof(records));
        }

        string owner = DnsRecordDataPresentation.NormalizeName(DnsWireNameCodec.Normalize(ownerName));
        SvcbRecord[] rrset = records.ToArray();
        if (rrset.Any(record => record.IsAliasMode)) {
            return Array.Empty<DnsServiceBinding>();
        }

        string[] protocols = (supportedAlpn ?? DefaultAlpn).Distinct(StringComparer.Ordinal).ToArray();
        var selected = new List<DnsServiceBinding>();
        foreach (SvcbRecord record in rrset.OrderBy(record => record.Priority)) {
            if (record.MandatoryKeys.Any(key => !UnderstoodKeys.Contains(key)) || record.Alpn.Count == 0 || record.Port == 0) {
                continue;
            }

            DnsDohUriTemplate? path = null;
            bool advertisesHttp = record.Alpn.Any(IsHttp);
            if ((advertisesHttp || record.Parameters.ContainsKey(7)) && !TryReadDohPath(record, out path)) {
                continue;
            }

            foreach (string protocol in protocols) {
                if (!record.Alpn.Contains(protocol, StringComparer.Ordinal) || !TryFormat(protocol, out DnsRequestFormat format)) {
                    continue;
                }

                string target = record.Target == "." ? owner : record.Target;
                selected.Add(new DnsServiceBinding(record, target, protocol, format, IsHttp(protocol) ? path : null));
            }
        }

        return selected.ToArray();
    }

    private static bool IsHttp(string protocol) => protocol == "h2" || protocol == "http/1.1" || protocol == "h3";
    private static bool TryFormat(string protocol, out DnsRequestFormat format) {
        format = protocol switch {
            "dot" => DnsRequestFormat.DnsOverTLS,
            "doq" => DnsRequestFormat.DnsOverQuic,
            "h2" or "http/1.1" => DnsRequestFormat.DnsOverHttps,
            "h3" => DnsRequestFormat.DnsOverHttp3,
            _ => DnsRequestFormat.DnsOverUDP
        };
        return protocol is "dot" or "doq" or "h2" or "http/1.1" or "h3";
    }

    private static bool TryReadDohPath(SvcbRecord record, out DnsDohUriTemplate? path) {
        path = null;
        if (!record.Parameters.TryGetValue(7, out SvcbParameter? parameter)) {
            return false;
        }

        try {
            string text = new UTF8Encoding(false, true).GetString(parameter.Bytes);
            path = DnsDohUriTemplate.Parse(text);
            if (!path.Variables.Contains("dns", StringComparer.Ordinal)) {
                return false;
            }

            foreach (string expansion in new[] {
                path.ExpandGet(new byte[] { 255 }),
                path.ExpandPost()
            }

            ) {
                // A :path cannot contain a fragment or replace the HTTPS authority.
                if (!expansion.StartsWith("/", StringComparison.Ordinal) || expansion.StartsWith("//", StringComparison.Ordinal) || expansion.IndexOf('#') >= 0 || expansion.IndexOf('\\') >= 0) {
                    return false;
                }

                if (!Uri.TryCreate("https://resolver.invalid" + expansion, UriKind.Absolute, out Uri? uri) || uri.Host != "resolver.invalid" || uri.Fragment.Length != 0) {
                    return false;
                }
            }

            return true;
        }
        catch (FormatException) {
            return false;
        }
        catch (DecoderFallbackException) {
            return false;
        }
    }
}
