using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace DnsClientX {
    /// <summary>Identifies the Windows policy store that supplied an NRPT rule.</summary>
    public enum SystemDnsPolicySource {
        /// <summary>The rule came from local computer policy.</summary>
        Local,

        /// <summary>The rule came from Group Policy.</summary>
        GroupPolicy
    }

    /// <summary>Represents a Windows Name Resolution Policy Table rule discovered by DnsClientX.</summary>
    public sealed class SystemDnsPolicyRule {
        private readonly ReadOnlyCollection<string> namespaces;
        private readonly ReadOnlyCollection<string> nameServers;

        internal SystemDnsPolicyRule(
            string id,
            SystemDnsPolicySource source,
            IEnumerable<string>? namespaces,
            IEnumerable<string>? nameServers,
            bool dnsSecValidationRequired,
            bool isSupported,
            string? diagnostic) {
            Id = id ?? string.Empty;
            Source = source;
            this.namespaces = Array.AsReadOnly((namespaces ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
            this.nameServers = Array.AsReadOnly((nameServers ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
            DnsSecValidationRequired = dnsSecValidationRequired;
            IsSupported = isSupported;
            Diagnostic = diagnostic;
        }

        /// <summary>Gets the registry subkey identifier for this rule.</summary>
        public string Id { get; }

        /// <summary>Gets the policy store that supplied this rule.</summary>
        public SystemDnsPolicySource Source { get; }

        /// <summary>Gets the namespaces configured for this rule.</summary>
        public IReadOnlyList<string> Namespaces => namespaces;

        /// <summary>Gets the generic DNS servers configured for this rule.</summary>
        public IReadOnlyList<string> NameServers => nameServers;

        /// <summary>Gets whether the policy requires DNSSEC validation.</summary>
        public bool DnsSecValidationRequired { get; }

        /// <summary>Gets whether DnsClientX can enforce every relevant option in this rule.</summary>
        public bool IsSupported { get; }

        /// <summary>Gets a diagnostic when the rule cannot be applied faithfully.</summary>
        public string? Diagnostic { get; }

        internal bool TryMatch(string queryName, out string matchedNamespace, out int specificity) {
            string canonicalQuery = CanonicalizeDnsName(queryName);
            matchedNamespace = string.Empty;
            specificity = -1;

            foreach (string configuredNamespace in namespaces) {
                string candidate = configuredNamespace.Trim();
                string canonicalNamespace;
                int candidateSpecificity;
                bool matches;

                if (candidate == ".") {
                    canonicalNamespace = ".";
                    candidateSpecificity = 0;
                    matches = true;
                } else if (candidate.StartsWith(".", StringComparison.Ordinal)) {
                    canonicalNamespace = CanonicalizeDnsName(candidate.Substring(1));
                    candidateSpecificity = 100000 + canonicalNamespace.Length;
                    matches = canonicalQuery.Equals(canonicalNamespace, StringComparison.OrdinalIgnoreCase)
                        || canonicalQuery.EndsWith("." + canonicalNamespace, StringComparison.OrdinalIgnoreCase);
                } else {
                    canonicalNamespace = CanonicalizeDnsName(candidate);
                    if (candidate.IndexOf('.') >= 0) {
                        candidateSpecificity = 300000 + canonicalNamespace.Length;
                        matches = canonicalQuery.Equals(canonicalNamespace, StringComparison.OrdinalIgnoreCase);
                    } else {
                        candidateSpecificity = 200000 + canonicalNamespace.Length;
                        int firstDot = canonicalQuery.IndexOf('.');
                        string firstLabel = firstDot < 0 ? canonicalQuery : canonicalQuery.Substring(0, firstDot);
                        matches = firstLabel.Equals(canonicalNamespace, StringComparison.OrdinalIgnoreCase);
                    }
                }

                if (matches && candidateSpecificity > specificity) {
                    matchedNamespace = candidate;
                    specificity = candidateSpecificity;
                }
            }

            return specificity >= 0;
        }

        internal static string CanonicalizeNamespaceExpression(string value) {
            string candidate = (value ?? string.Empty).Trim();
            if (candidate == ".") return ".";
            bool suffix = candidate.StartsWith(".", StringComparison.Ordinal);
            string canonical = CanonicalizeDnsName(suffix ? candidate.Substring(1) : candidate);
            return suffix ? "." + canonical : canonical;
        }

        private static string CanonicalizeDnsName(string value) {
            string trimmed = (value ?? string.Empty).Trim().TrimEnd('.');
            if (trimmed.Length == 0) return string.Empty;

            var idn = new IdnMapping();
            string[] labels = trimmed.Split('.');
            for (int index = 0; index < labels.Length; index++) {
                try {
                    labels[index] = idn.GetAscii(labels[index]);
                } catch (ArgumentException) {
                    labels[index] = labels[index].ToLowerInvariant();
                }
            }
            return string.Join(".", labels).ToLowerInvariant();
        }
    }

    /// <summary>Describes the effective NRPT match for one query name.</summary>
    public sealed class SystemDnsPolicyMatch {
        internal SystemDnsPolicyMatch(
            string ruleId,
            SystemDnsPolicySource source,
            string matchedNamespace,
            IReadOnlyList<string> nameServers,
            bool dnsSecValidationRequired,
            bool canApply,
            string? diagnostic) {
            RuleId = ruleId;
            Source = source;
            MatchedNamespace = matchedNamespace;
            NameServers = nameServers;
            DnsSecValidationRequired = dnsSecValidationRequired;
            CanApply = canApply;
            Diagnostic = diagnostic;
        }

        /// <summary>Gets the registry subkey identifier for the matched rule.</summary>
        public string RuleId { get; }

        /// <summary>Gets the policy store that supplied the matched rule.</summary>
        public SystemDnsPolicySource Source { get; }

        /// <summary>Gets the namespace expression that matched the query.</summary>
        public string MatchedNamespace { get; }

        /// <summary>Gets the policy-specific DNS servers, if any.</summary>
        public IReadOnlyList<string> NameServers { get; }

        /// <summary>Gets whether this policy requires DNSSEC validation.</summary>
        public bool DnsSecValidationRequired { get; }

        /// <summary>Gets whether DnsClientX can apply this match faithfully.</summary>
        public bool CanApply { get; }

        /// <summary>Gets a diagnostic when the match cannot be applied.</summary>
        public string? Diagnostic { get; }
    }
}
