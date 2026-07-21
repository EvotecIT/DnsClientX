using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX;

/// <summary>Describes a DNSSEC DS trust anchor used by local validation.</summary>
public readonly struct DnsSecTrustAnchor {
    /// <summary>Gets the DNSKEY key tag.</summary>
    public ushort KeyTag { get; }

    /// <summary>Gets the DNSKEY algorithm.</summary>
    public DnsKeyAlgorithm Algorithm { get; }

    /// <summary>Gets the DS digest type.</summary>
    public byte DigestType { get; }

    /// <summary>Gets the uppercase hexadecimal DS digest.</summary>
    public string Digest { get; }

    /// <summary>Gets the instant from which IANA declares the anchor valid.</summary>
    public DateTimeOffset? ValidFrom { get; }

    /// <summary>Gets the instant after which IANA declares the anchor invalid, when specified.</summary>
    public DateTimeOffset? ValidUntil { get; }

    /// <summary>Initializes a trust-anchor value.</summary>
    public DnsSecTrustAnchor(ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest,
        DateTimeOffset? validFrom = null, DateTimeOffset? validUntil = null) {
        KeyTag = keyTag;
        Algorithm = algorithm;
        DigestType = digestType;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    /// <summary>Determines whether the anchor is valid at the supplied instant.</summary>
    public bool IsValidAt(DateTimeOffset instant) =>
        (!ValidFrom.HasValue || instant >= ValidFrom.Value) &&
        (!ValidUntil.HasValue || instant < ValidUntil.Value);
}

/// <summary>Exposes the IANA root trust anchors bundled with this DnsClientX release.</summary>
public static class DnsSecTrustAnchors {
    private static readonly DnsSecTrustAnchor[] Anchors = {
        new(20326, DnsKeyAlgorithm.RSASHA256, 2,
            "E06D44B80B8F1D39A95C0B0D7C65D08458E880409BBC683457104237C7F8EC8D",
            new DateTimeOffset(2017, 2, 2, 0, 0, 0, TimeSpan.Zero)),
        new(38696, DnsKeyAlgorithm.RSASHA256, 2,
            "683D2D0ACB8C9B712A1948B27F741219298D0A450D612C483AF444A4C0FB2B16",
            new DateTimeOffset(2024, 7, 18, 0, 0, 0, TimeSpan.Zero))
    };

    /// <summary>Gets an immutable view of the bundled root DS records.</summary>
    public static IReadOnlyList<DnsSecTrustAnchor> Current { get; } = Array.AsReadOnly(Anchors);
}

/// <summary>
/// Represents a DNSSEC DS record used as a trust anchor.
/// </summary>
internal readonly struct RootDsRecord
{
    /// <summary>Key tag of the DNSKEY record.</summary>
    public ushort KeyTag { get; }

    /// <summary>DNSKEY algorithm identifier.</summary>
    public DnsKeyAlgorithm Algorithm { get; }

    /// <summary>Digest type as defined by RFC 4034.</summary>
    public byte DigestType { get; }

    /// <summary>Hex-encoded digest value.</summary>
    public string Digest { get; }

    public DateTimeOffset? ValidFrom { get; }

    public DateTimeOffset? ValidUntil { get; }

    public RootDsRecord(ushort keyTag, DnsKeyAlgorithm algorithm, byte digestType, string digest,
        DateTimeOffset? validFrom = null, DateTimeOffset? validUntil = null)
    {
        KeyTag = keyTag;
        Algorithm = algorithm;
        DigestType = digestType;
        Digest = digest;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    public bool IsValidAt(DateTimeOffset instant) =>
        (!ValidFrom.HasValue || instant >= ValidFrom.Value) &&
        (!ValidUntil.HasValue || instant < ValidUntil.Value);
}

/// <summary>
/// Collection of built-in root trust anchors for DNSSEC validation.
/// </summary>
internal static class RootTrustAnchors
{
    /// <summary>Default DS records for the DNS root.</summary>
    internal static readonly RootDsRecord[] DsRecords = DnsSecTrustAnchors.Current
        .Select(anchor => new RootDsRecord(anchor.KeyTag, anchor.Algorithm, anchor.DigestType, anchor.Digest,
            anchor.ValidFrom, anchor.ValidUntil))
        .ToArray();
}
