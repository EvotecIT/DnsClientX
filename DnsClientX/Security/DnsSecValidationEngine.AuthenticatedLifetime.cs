using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    internal sealed partial class DnsSecValidationEngine {
        /// <summary>Earliest expiry of the authenticated answer, denial, and trust-chain RRsets.</summary>
        internal DateTimeOffset? CacheExpiresAtUtc { get; private set; }
        private readonly Dictionary<RrsetKey, DateTimeOffset> _authenticatedLifetimes = new();
        private readonly List<DnsSecSignature> _authenticatedSignatures = new();

        private DnsSecValidationResult CompleteValidation(DnsResponse response, DnsSecValidationResult result) {
            if (result.Status != DnsSecValidationStatus.Secure) response.DnsSecCompactDenial = false;
            if ((result.Status == DnsSecValidationStatus.Secure || result.Status == DnsSecValidationStatus.Insecure)
                && _authenticatedSignatures.Any(signature => !DnsSecWire.SignatureTimeIsValid(signature, ValidationTime))) {
                response.DnsSecCompactDenial = false;
                return DnsSecValidationResult.Indeterminate("An authenticated dependency's signature expired before validation completed.");
            }
            if (result.Status == DnsSecValidationStatus.Secure || result.Status == DnsSecValidationStatus.Insecure) {
                ApplyAuthenticatedLifetimes(response);
            }
            return result;
        }

        private void RecordAuthenticatedLifetime(DnsResponse response, RrsetKey rrset, DnsSecSignature signature) {
            _authenticatedSignatures.Add(signature);
            DnsWireResourceRecord[] records = DnsSecWire.Records(response);
            uint ttl = records.Where(record => record.Type == rrset.Type && record.Class == rrset.Class
                && string.Equals(DnsWireNameCodec.Canonical(record.Name), rrset.Name, StringComparison.Ordinal))
                .Min(record => CacheableTtl(record.RawTtl));
            uint signatureTtl = records.Where(record => record.Type == DnsRecordType.RRSIG
                && record.Class == rrset.Class && DnsSecWire.TryReadSignature(response.WireMessage, record, out var candidate)
                && candidate.Owner == signature.Owner && candidate.TypeCovered == signature.TypeCovered
                && candidate.Signature.SequenceEqual(signature.Signature))
                .Min(record => CacheableTtl(record.RawTtl));
            // Signature timestamps use RFC 1982 serial arithmetic, including the 2106 wrap.
            DateTimeOffset now = ValidationTime;
            long seconds = now.ToUnixTimeSeconds();
            DateTimeOffset signatureDeadline = DateTimeOffset.FromUnixTimeSeconds(
                seconds + unchecked((int)(signature.Expiration - (uint)seconds)));
            ttl = Math.Min(Math.Min(ttl, signatureTtl), CacheableTtl(signature.OriginalTtl));
            DateTimeOffset expires = (response.ReceivedAtUtc ?? _now).AddSeconds(ttl);
            if (signatureDeadline < expires) expires = signatureDeadline;
            if (!CacheExpiresAtUtc.HasValue || expires < CacheExpiresAtUtc.Value) CacheExpiresAtUtc = expires;
            RetainDeadline(rrset, expires);
            if (rrset.Type == DnsRecordType.DNAME) {
                var answers = response.WireAnswers ?? Array.Empty<DnsWireResourceRecord>();
                var selected = answers.Where(record => record.Type != DnsRecordType.DNAME
                    || string.Equals(DnsWireNameCodec.Canonical(record.Name), rrset.Name, StringComparison.Ordinal)).ToArray();
                foreach (var cname in answers.Where(record => record.Type == DnsRecordType.CNAME && IsSynthesizedDnameCname(selected, record))) {
                    RetainDeadline(new RrsetKey(DnsWireNameCodec.Canonical(cname.Name), DnsRecordType.CNAME, cname.Class), expires);
                }
            }
        }

        private void RetainDeadline(RrsetKey rrset, DateTimeOffset expires) {
            if (!_authenticatedLifetimes.TryGetValue(rrset, out var current) || expires < current) {
                _authenticatedLifetimes[rrset] = expires;
            }
        }

        /// <summary>Clamps also the copies made when iterative alias segments are merged.</summary>
        internal void ApplyAuthenticatedLifetimes(DnsResponse response) {
            DateTimeOffset now = ValidationTime;
            var byRecord = new Dictionary<(string Name, DnsRecordType Type), DateTimeOffset>();
            var byOwner = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
            foreach (var lifetime in _authenticatedLifetimes) {
                var key = (lifetime.Key.Name, lifetime.Key.Type);
                if (!byRecord.TryGetValue(key, out var recordExpiry) || lifetime.Value < recordExpiry) byRecord[key] = lifetime.Value;
                if (!byOwner.TryGetValue(key.Name, out var ownerExpiry) || lifetime.Value < ownerExpiry) byOwner[key.Name] = lifetime.Value;
            }
            Clamp(response.Answers);
            Clamp(response.Authorities);
            Clamp(response.Additional);
            response.RefreshDerivedData();
            void Clamp(DnsAnswer[]? answers) {
                if (answers == null) return;
                for (int i = 0; i < answers.Length; i++) {
                    string owner = string.IsNullOrEmpty(answers[i].Name) ? answers[i].OriginalName : answers[i].Name;
                    if (string.IsNullOrEmpty(owner)) continue;
                    string canonical = DnsWireNameCodec.Canonical(owner);
                    bool authenticated = answers[i].Type == DnsRecordType.RRSIG
                        ? byOwner.TryGetValue(canonical, out var expires)
                        : byRecord.TryGetValue((canonical, answers[i].Type), out expires);
                    if (authenticated) {
                        int remaining = (int)Math.Min(int.MaxValue, Math.Max(0, (expires - now).TotalSeconds));
                        answers[i].TTL = Math.Min(Math.Max(0, answers[i].TTL), remaining);
                    }
                }
            }
        }

        private static uint CacheableTtl(uint ttl) => ttl > int.MaxValue ? 0 : ttl;
    }
}
