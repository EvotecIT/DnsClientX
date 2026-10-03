using System;
using System.Linq;

namespace DnsClientX {
    internal sealed partial class DnsSecValidationEngine {
        /// <summary>Earliest expiry of the authenticated answer, denial, and trust-chain RRsets.</summary>
        internal DateTimeOffset? CacheExpiresAtUtc { get; private set; }

        private void RecordAuthenticatedLifetime(DnsResponse response, RrsetKey rrset, DnsSecSignature signature) {
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
            long seconds = _now.ToUnixTimeSeconds();
            DateTimeOffset signatureDeadline = DateTimeOffset.FromUnixTimeSeconds(
                seconds + unchecked((int)(signature.Expiration - (uint)seconds)));
            ttl = Math.Min(Math.Min(ttl, signatureTtl), CacheableTtl(signature.OriginalTtl));
            double lifetime = Math.Min(ttl, Math.Max(0, (signatureDeadline - _now).TotalSeconds));
            DateTimeOffset expires = _now.AddSeconds(lifetime);
            if (!CacheExpiresAtUtc.HasValue || expires < CacheExpiresAtUtc.Value) CacheExpiresAtUtc = expires;

            Clamp(response.Answers);
            Clamp(response.Authorities);
            Clamp(response.Additional);
            void Clamp(DnsAnswer[]? answers) {
                if (answers == null) return;
                for (int i = 0; i < answers.Length; i++) {
                    string owner = string.IsNullOrEmpty(answers[i].Name) ? answers[i].OriginalName : answers[i].Name;
                    if (!string.IsNullOrEmpty(owner) && (answers[i].Type == rrset.Type || answers[i].Type == DnsRecordType.RRSIG)
                        && string.Equals(DnsWireNameCodec.Canonical(owner), rrset.Name, StringComparison.Ordinal)) {
                        answers[i].TTL = Math.Min(Math.Max(0, answers[i].TTL), (int)lifetime);
                    }
                }
            }
        }

        private static uint CacheableTtl(uint ttl) => ttl > int.MaxValue ? 0 : ttl;
    }
}
