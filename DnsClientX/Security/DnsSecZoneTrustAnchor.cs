using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DnsClientX;

/// <summary>Associates explicitly trusted DS material with one DNSSEC zone.</summary>
/// <remarks>A configured zone begins its own trust chain without requiring its parent's DS record.
/// Descendant zones still require authenticated delegations. Obtain this material through a trusted channel.</remarks>
public sealed class DnsSecZoneTrustAnchor {
    /// <summary>Creates an immutable zone-qualified DS anchor.</summary>
    /// <param name="zone">The exact zone apex, including the root when explicitly configured.</param>
    /// <param name="anchor">The DS identity and optional validity interval.</param>
    public DnsSecZoneTrustAnchor(string zone, DnsSecTrustAnchor anchor) {
        if (string.IsNullOrWhiteSpace(zone)) throw new ArgumentException("A trust-anchor zone is required.", nameof(zone));
        Zone = DnsWireNameCodec.Canonical(zone);
        DnsWireNameCodec.EncodeLabels(Zone);
        if ((int)anchor.Algorithm < 0 || (int)anchor.Algorithm > byte.MaxValue) throw new ArgumentException("A DNSSEC algorithm identifier must fit its wire byte.", nameof(anchor));
        string digest = (anchor.Digest ?? string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        if (digest.Length == 0 || (digest.Length & 1) != 0 || digest.Any(character => !Uri.IsHexDigit(character))) {
            throw new ArgumentException("A DS trust anchor requires a hexadecimal digest.", nameof(anchor));
        }
        int expected = anchor.DigestType == 1 ? 40 : anchor.DigestType == 2 ? 64 : anchor.DigestType == 4 ? 96 : 0;
        if (expected != 0 && digest.Length != expected) throw new ArgumentException("The DS digest length does not match its digest type.", nameof(anchor));
        if (anchor.ValidFrom.HasValue && anchor.ValidUntil.HasValue && anchor.ValidFrom >= anchor.ValidUntil) {
            throw new ArgumentException("The trust-anchor validity interval is empty.", nameof(anchor));
        }
        Anchor = new DnsSecTrustAnchor(anchor.KeyTag, anchor.Algorithm, anchor.DigestType, digest,
            anchor.ValidFrom, anchor.ValidUntil, anchor.PublicKey, anchor.Flags);
    }

    /// <summary>Gets the canonical zone apex authenticated by this anchor.</summary>
    public string Zone { get; }
    /// <summary>Gets the trusted DS material and validity interval.</summary>
    public DnsSecTrustAnchor Anchor { get; }

    internal static IReadOnlyList<DnsSecZoneTrustAnchor>? Snapshot(IReadOnlyList<DnsSecZoneTrustAnchor>? anchors) {
        if (anchors == null) return null;
        DnsSecZoneTrustAnchor[] snapshot = anchors.ToArray();
        if (snapshot.Any(anchor => anchor == null)) throw new ArgumentException("A zone trust-anchor collection cannot contain null entries.", nameof(anchors));
        return Array.AsReadOnly(snapshot);
    }

    internal static string CacheIdentity(IReadOnlyList<DnsSecZoneTrustAnchor>? anchors) {
        if (anchors == null || anchors.Count == 0) return string.Empty;
        var rows = anchors.Select(value => {
            if (value == null) throw new ArgumentException("A zone trust-anchor collection cannot contain null entries.", nameof(anchors));
            DnsSecTrustAnchor anchor = value.Anchor;
            string[] fields = { value.Zone, anchor.KeyTag.ToString(CultureInfo.InvariantCulture), ((byte)anchor.Algorithm).ToString(CultureInfo.InvariantCulture),
                anchor.DigestType.ToString(CultureInfo.InvariantCulture), anchor.Digest, anchor.Flags.ToString(CultureInfo.InvariantCulture),
                anchor.ValidFrom?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                anchor.ValidUntil?.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty };
            var row = new StringBuilder();
            foreach (string field in fields) DnsCacheKeyBuilder.Append(row, field);
            return row.ToString();
        }).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal);
        using SHA256 hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
    }
}
