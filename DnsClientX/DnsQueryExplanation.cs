using System;
using System.Linq;

namespace DnsClientX {
    /// <summary>Describes only the transport and DNS evidence captured for a completed response.</summary>
    public sealed class DnsQueryExplanation {
        /// <summary>Gets the actual response source, including cache and coalesced requests.</summary>
        public DnsResponseSource ResponseSource { get; private set; }
        /// <summary>Gets the selected resolver when captured.</summary>
        public string? Resolver { get; private set; }
        /// <summary>Gets the observed transport.</summary>
        public Transport Transport { get; private set; }
        /// <summary>Gets the response code.</summary>
        public DnsResponseCode Status { get; private set; }
        /// <summary>Gets the normalized failure category.</summary>
        public DnsQueryErrorCode ErrorCode { get; private set; }
        /// <summary>Gets the captured retry count.</summary>
        public int RetryCount { get; private set; }
        /// <summary>Gets endpoint hostname/bootstrap provenance when available.</summary>
        public DnsServerResolutionInfo? Bootstrap { get; private set; }
        /// <summary>Gets the resolver's AD assertion; this is distinct from local validation.</summary>
        public bool ResolverAuthenticData { get; private set; }
        /// <summary>Gets the local DNSSEC verdict.</summary>
        public DnsSecValidationStatus DnsSecValidationStatus { get; private set; }
        /// <summary>Gets the captured explanation of local DNSSEC validation.</summary>
        public string DnsSecValidationMessage { get; private set; } = string.Empty;
        /// <summary>Gets recorded recursive DNSSEC material lookups, rather than an inferred ordered trust graph.</summary>
        public DnsSecValidationDependency[] DnsSecValidationDependencies { get; private set; } = Array.Empty<DnsSecValidationDependency>();
        /// <summary>Gets aliases present in the original wire answer, or the visible answer when wire evidence is unavailable.</summary>
        public DnsAnswer[] Aliases { get; private set; } = Array.Empty<DnsAnswer>();
        /// <summary>Gets authority/delegation hints present in the returned message. They are not a complete traversal.</summary>
        public DnsAnswer[] AuthorityHints { get; private set; } = Array.Empty<DnsAnswer>();
        /// <summary>Gets DNSSEC material present in the response sections. Presence alone is not verification.</summary>
        public DnsAnswer[] DnsSecRecords { get; private set; } = Array.Empty<DnsAnswer>();
        /// <summary>Gets structured audit attempts when the caller supplied captured audit evidence.</summary>
        public DnsQueryAttemptExplanation[] Attempts { get; private set; } = Array.Empty<DnsQueryAttemptExplanation>();
        /// <summary>Gets the number of recorded minimized delegation queries.</summary>
        public int QNameMinimizedQueryCount { get; private set; }
        /// <summary>Gets the number of recorded full-question fallbacks.</summary>
        public int QNameMinimizationFallbackCount { get; private set; }
        /// <summary>Gets unavailable evidence boundaries so consumers cannot mistake response hints for a full chain.</summary>
        public string[] UnavailableEvidence { get; private set; } = Array.Empty<string>();

        /// <summary>Projects captured evidence without making additional DNS requests.</summary>
        public static DnsQueryExplanation Create(DnsResponse response, AuditEntry[]? auditTrail = null) {
            if (response == null) throw new ArgumentNullException(nameof(response));
            DnsAnswer[] answers = response.WireAnswers.Length > 0 ? response.WireAnswers.Select(r => new DnsAnswer { Name = r.Name, Type = r.Type, TTL = r.Ttl, DataRaw = r.Data }).ToArray() : response.Answers ?? Array.Empty<DnsAnswer>();
            DnsAnswer[] authorities = response.Authorities ?? Array.Empty<DnsAnswer>();
            var unavailable = new System.Collections.Generic.List<string> { "Complete delegation traversal was not recorded.", "DNSSEC verdict is available; a complete ordered trust chain was not recorded." };
            if (auditTrail == null || auditTrail.Length == 0) unavailable.Add("Individual attempt audit was not supplied.");
            if (response.ServerResolution == null) unavailable.Add("Endpoint hostname/bootstrap resolution evidence was not recorded.");
            if (response.WireAnswers.Length == 0) unavailable.Add("Original wire answer was unavailable; alias hints use the visible response.");
            return new DnsQueryExplanation {
                ResponseSource = response.ResponseSource, Resolver = response.ServerAddress, Transport = response.UsedTransport,
                Status = response.EffectiveStatus, ErrorCode = response.ErrorCode, RetryCount = response.RetryCount, Bootstrap = response.ServerResolution,
                ResolverAuthenticData = response.AuthenticData, DnsSecValidationStatus = response.DnsSecValidationStatus,
                DnsSecValidationMessage = response.DnsSecValidationMessage,
                DnsSecValidationDependencies = response.DnsSecValidationDependencies.ToArray(),
                Aliases = answers.Where(a => a.Type == DnsRecordType.CNAME || a.Type == DnsRecordType.DNAME).ToArray(),
                AuthorityHints = authorities.Where(a => a.Type == DnsRecordType.NS || a.Type == DnsRecordType.SOA).ToArray(),
                DnsSecRecords = answers.Concat(authorities).Concat(response.Additional ?? Array.Empty<DnsAnswer>()).Where(a => a.Type == DnsRecordType.DNSKEY || a.Type == DnsRecordType.DS || a.Type == DnsRecordType.RRSIG || a.Type == DnsRecordType.NSEC || a.Type == DnsRecordType.NSEC3).ToArray(),
                Attempts = (auditTrail ?? Array.Empty<AuditEntry>()).Select(DnsQueryAttemptExplanation.Create).ToArray(),
                QNameMinimizedQueryCount = response.QNameMinimizedQueryCount, QNameMinimizationFallbackCount = response.QNameMinimizationFallbackCount,
                UnavailableEvidence = unavailable.ToArray()
            };
        }
    }
}
