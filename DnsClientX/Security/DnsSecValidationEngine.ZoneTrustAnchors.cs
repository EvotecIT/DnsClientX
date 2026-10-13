using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX;

internal sealed partial class DnsSecValidationEngine {
    private readonly List<DnsSecZoneTrustAnchor> _authenticatedZoneAnchors = new();
    private DateTimeOffset? _zoneTrustDeadline;

    private string? ZoneTrustBoundary(string owner, DnsRecordType type) => _zoneTrustAnchors
        .Where(anchor => IsNameWithinZone(owner, anchor.Zone)
            // DS belongs to the parent side of a cut, including an explicitly anchored apex.
            && (type != DnsRecordType.DS || DnsWireNameCodec.Canonical(owner) != anchor.Zone))
        .OrderByDescending(anchor => DnsWireNameCodec.CanonicalLabels(anchor.Zone).Length)
        .Select(anchor => anchor.Zone).FirstOrDefault();

    private bool IsPermittedByZoneTrustBoundary(string owner, DnsRecordType type, string signer) {
        string? boundary = ZoneTrustBoundary(owner, type);
        return boundary == null || IsNameWithinZone(signer, boundary);
    }

    private ZoneKeysResult ValidateConfiguredZoneKeys(string zone, DnsResponse response, RrsetKey rrset,
        IReadOnlyList<DnsSecSignature> signatures, IReadOnlyList<DnsSecKey> keys,
        IReadOnlyList<DnsSecZoneTrustAnchor> configured) {
        DateTimeOffset now = ValidationTime;
        var active = configured.Where(item => item.Anchor.IsValidAt(now) && (item.Anchor.Flags & 0x0080) == 0)
            .OrderByDescending(item => item.Anchor.ValidUntil ?? DateTimeOffset.MaxValue).ToArray();
        if (active.Length == 0) return ZoneKeysResult.Bogus($"Every configured trust anchor for {zone} is expired, not yet valid, or revoked.");
        bool supported = false;
        DnsSecValidationResult? signatureFailure = null;
        foreach (DnsSecZoneTrustAnchor item in active) {
            DnsSecTrustAnchor anchor = item.Anchor;
            if ((anchor.DigestType != 1 && anchor.DigestType != 2 && anchor.DigestType != 4)
                || !DnsSecCrypto.IsSupportedAlgorithm((byte)anchor.Algorithm, _signatureVerifier)) continue;
            supported = true;
            foreach (DnsSecKey key in keys.Where(key => key.KeyTag == anchor.KeyTag && key.Algorithm == (byte)anchor.Algorithm)) {
                if (!DnsSecCrypto.TryComputeDsDigest(zone, key, anchor.DigestType, out byte[] digest)
                    || !string.Equals(ToHex(digest), anchor.Digest, StringComparison.Ordinal)) continue;
                DnsSecValidationResult verified = VerifyRrset(response, rrset, signatures, new[] { key });
                if (verified.Status != DnsSecValidationStatus.Secure) { signatureFailure = verified; continue; }
                _authenticatedZoneAnchors.Add(item);
                if (anchor.ValidUntil.HasValue) {
                    if (!_zoneTrustDeadline.HasValue || anchor.ValidUntil.Value < _zoneTrustDeadline.Value) _zoneTrustDeadline = anchor.ValidUntil;
                    if (!CacheExpiresAtUtc.HasValue || anchor.ValidUntil.Value < CacheExpiresAtUtc.Value) CacheExpiresAtUtc = anchor.ValidUntil;
                }
                return ZoneKeysResult.Secure(keys, $"The {zone} DNSKEY RRset is signed by a key matching its configured zone trust anchor.");
            }
        }
        if (signatureFailure.HasValue) return new ZoneKeysResult(signatureFailure.Value.Status, Array.Empty<DnsSecKey>(), signatureFailure.Value.Message);
        return supported ? ZoneKeysResult.Bogus($"No usable DNSKEY for {zone} matched its configured trust-anchor digest.")
            : ZoneKeysResult.Indeterminate($"The configured trust anchors for {zone} use no supported digest/key combination.");
    }
}
