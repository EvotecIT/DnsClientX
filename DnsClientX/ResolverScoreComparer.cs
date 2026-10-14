using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>Compares captured resolver evidence without treating consensus as authoritative correctness.</summary>
    public static class ResolverScoreComparer {
        /// <summary>Compares candidates and query observations. Health/performance conclusions require matching recorded scopes.</summary>
        public static ResolverScoreComparison Compare(ResolverScoreSnapshot baseline, ResolverScoreSnapshot current, ResolverAnswerReference[]? authoritativeReferences = null) {
            ValidateSnapshot(baseline); ValidateSnapshot(current);
            ResolverAnswerReference[] references = authoritativeReferences ?? Array.Empty<ResolverAnswerReference>();
            foreach (ResolverAnswerReference reference in references) {
                if (reference == null || string.IsNullOrWhiteSpace(reference.Name) || reference.Response == null || !reference.Response.IsAuthoritativeAnswer) throw new ArgumentException("Each authoritative reference needs a query name and an AA response.", nameof(authoritativeReferences));
                if (reference.Response.Questions?.Length > 0 && !reference.Response.Questions.Any(q => Canonical(q.Name) == Canonical(reference.Name) && q.Type == reference.RecordType)) throw new ArgumentException("A reference response question must match its name and type.", nameof(authoritativeReferences));
            }
            if (references.GroupBy(r => (Canonical(r.Name), r.RecordType)).Any(g => g.Count() > 1)) throw new ArgumentException("Only one reference per query name/type is allowed.", nameof(authoritativeReferences));
            var referenceSignatures = references.ToDictionary(reference => (Canonical(reference.Name), reference.RecordType), reference => {
                // Match the normal query's terminal RRset projection while preserving the caller's raw envelope.
                DnsResponse projected = reference.Response.Clone();
                ClientX.ApplyAnswerProjection(projected, reference.Name, reference.RecordType, returnAllTypes: false);
                return DnsResponseAnswerSignature.Build(projected);
            });
            string[] reasons = GetScopeReasons(baseline, current).ToArray();
            bool comparable = reasons.Length == 0;
            Dictionary<string, ResolverScoreEntry[]> before = baseline.Results.GroupBy(e => ResolverCandidateIdentity.GroupKey(e.CandidateId, e.Target), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ResolverScoreEntry[]> after = current.Results.GroupBy(e => ResolverCandidateIdentity.GroupKey(e.CandidateId, e.Target), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
            ResolverScoreChange[] candidates = before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).Select(key => {
                before.TryGetValue(key, out ResolverScoreEntry[]? oldEntries); after.TryGetValue(key, out ResolverScoreEntry[]? newEntries);
                if (oldEntries?.Length > 1 || newEntries?.Length > 1 ||
                    oldEntries != null && HasUnavailableIdentity(baseline.Context, oldEntries![0]) || newEntries != null && HasUnavailableIdentity(current.Context, newEntries![0])) {
                    return new ResolverScoreChange { Target = newEntries?.FirstOrDefault()?.Target ?? oldEntries![0].Target, CandidateId = newEntries?.FirstOrDefault()?.CandidateId ?? oldEntries![0].CandidateId, Change = "Ambiguous" };
                }
                ResolverScoreEntry? oldEntry = oldEntries?.Single(), newEntry = newEntries?.Single();
                return new ResolverScoreChange {
                    Target = newEntry?.Target ?? oldEntry!.Target, CandidateId = newEntry?.CandidateId ?? oldEntry!.CandidateId, Baseline = oldEntry, Current = newEntry,
                    Change = oldEntry == null ? "Added" : newEntry == null ? "Removed" : SameScore(oldEntry, newEntry) ? "Unchanged" : "Changed",
                    SuccessPercentDelta = !comparable || oldEntry == null || newEntry == null ? null : newEntry.SuccessPercent - oldEntry.SuccessPercent,
                    AverageMsDelta = !comparable || oldEntry == null || newEntry == null || oldEntry.SuccessCount == 0 || newEntry.SuccessCount == 0 ? null : newEntry.AverageMs - oldEntry.AverageMs
                };
            }).ToArray();
            ResolverAnswerObservation[] oldAnswers = baseline.Answers ?? Array.Empty<ResolverAnswerObservation>();
            ResolverAnswerObservation[] newAnswers = current.Answers ?? Array.Empty<ResolverAnswerObservation>();
            var keys = oldAnswers.Concat(newAnswers).Select(a => (Identity: ResolverCandidateIdentity.GroupKey(a.CandidateId, a.Target).ToLowerInvariant(), Name: Canonical(a.Name), a.RecordType)).Distinct();
            ResolverAnswerChange[] answers = keys.OrderBy(k => k.Identity, StringComparer.Ordinal).ThenBy(k => k.Name, StringComparer.Ordinal).ThenBy(k => k.RecordType).Select(key => {
                ResolverAnswerObservation[] oldValues = oldAnswers.Where(a => ResolverCandidateIdentity.GroupKey(a.CandidateId, a.Target).Equals(key.Identity, StringComparison.OrdinalIgnoreCase) && Canonical(a.Name) == key.Name && a.RecordType == key.RecordType).ToArray();
                ResolverAnswerObservation[] newValues = newAnswers.Where(a => ResolverCandidateIdentity.GroupKey(a.CandidateId, a.Target).Equals(key.Identity, StringComparison.OrdinalIgnoreCase) && Canonical(a.Name) == key.Name && a.RecordType == key.RecordType).ToArray();
                ResolverAnswerReference? reference = references.FirstOrDefault(r => Canonical(r.Name) == key.Name && r.RecordType == key.RecordType);
                int matches = reference == null ? 0 : newValues.Count(a => a.Status == reference.Response.Status.ToString() && a.AnswerSignature == referenceSignatures[(key.Name, key.RecordType)]);
                return new ResolverAnswerChange {
                    Target = newValues.FirstOrDefault()?.Target ?? oldValues[0].Target, CandidateId = newValues.FirstOrDefault()?.CandidateId ?? oldValues[0].CandidateId, Name = key.Name, RecordType = key.RecordType,
                    Baseline = oldValues, Current = newValues,
                    Changed = oldValues.Length == 0 || newValues.Length == 0 ? null : !Variants(oldValues).SequenceEqual(Variants(newValues), StringComparer.Ordinal),
                    ReferenceSource = reference?.Source,
                    ReferenceComparison = reference == null || newValues.Length == 0 ? "Unavailable" : matches == newValues.Length ? "Matches" : matches == 0 ? "Differs" : "Mixed"
                };
            }).ToArray();
            return new ResolverScoreComparison { ScopeComparable = comparable, IncomparableReasons = reasons,
                BaselinePolicyPassed = baseline.Summary.PolicyPassed, CurrentPolicyPassed = current.Summary.PolicyPassed,
                HealthRegression = comparable && baseline.Summary.PolicyPassed && !current.Summary.PolicyPassed, Candidates = candidates, Answers = answers };
        }

        private static string Canonical(string name) => (name ?? string.Empty).TrimEnd('.').ToLowerInvariant();
        private static IEnumerable<string> Variants(IEnumerable<ResolverAnswerObservation> observations) => observations.Select(a => a.Status.Length + ":" + a.Status + a.AnswerSignature).Distinct(StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal);
        private static bool SameScore(ResolverScoreEntry a, ResolverScoreEntry b) => a.Resolver == b.Resolver && a.Transport == b.Transport && a.TotalQueries == b.TotalQueries && a.SuccessCount == b.SuccessCount && a.AverageMs == b.AverageMs && a.Rank == b.Rank && a.IsRecommended == b.IsRecommended;
        private static void ValidateSnapshot(ResolverScoreSnapshot snapshot) {
            if (!ResolverScoreSnapshot.TryValidateCompatibility(snapshot, out string? error)) throw new ArgumentException(error);
            if (snapshot.Summary == null || snapshot.Summary.Domains == null || snapshot.Summary.RecordTypes == null || snapshot.Results == null || snapshot.Results.Any(r => r == null || string.IsNullOrWhiteSpace(r.Target))) throw new ArgumentException("Snapshot query catalog, summary, and named candidates are required.");
            if (snapshot.Answers?.Any(a => a == null || string.IsNullOrWhiteSpace(a.Target) || string.IsNullOrWhiteSpace(a.Name) || a.Status == null || a.AnswerSignature == null) == true) throw new ArgumentException("Snapshot answer observations must be complete.");
        }
        private static IEnumerable<string> GetScopeReasons(ResolverScoreSnapshot a, ResolverScoreSnapshot b) {
            if (a.Results.GroupBy(r => ResolverCandidateIdentity.GroupKey(r.CandidateId, r.Target), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) || b.Results.GroupBy(r => ResolverCandidateIdentity.GroupKey(r.CandidateId, r.Target), StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) yield return "Candidate labels are ambiguous across multiple resolver observations.";
            if (a.Answers == null || b.Answers == null || a.Answers.Length == 0 || b.Answers.Length == 0) yield return "One snapshot lacks correlated answer observations.";
            if (a.Context == null || b.Context == null) yield return "One snapshot lacks host/runtime/execution context.";
            else {
                bool firstAvailable = TryGetExecutionKeys(a.Context, a.Results, out Dictionary<string, string> firstKeys);
                bool secondAvailable = TryGetExecutionKeys(b.Context, b.Results, out Dictionary<string, string> secondKeys);
                bool identitiesAvailable = firstAvailable && secondAvailable;
                if (!identitiesAvailable) yield return "One snapshot lacks canonical candidate execution identities.";
                else if (firstKeys.Count != secondKeys.Count || firstKeys.Any(pair => !secondKeys.TryGetValue(pair.Key, out string? key) || key != pair.Value)) yield return "Resolver execution identity differs.";
                if (string.IsNullOrWhiteSpace(a.Context.Host) || string.IsNullOrWhiteSpace(b.Context.Host) || !string.Equals(a.Context.Host, b.Context.Host, StringComparison.OrdinalIgnoreCase)) yield return "Host differs or is unavailable.";
                if (string.IsNullOrWhiteSpace(a.Context.Runtime) || string.IsNullOrWhiteSpace(b.Context.Runtime) || string.IsNullOrWhiteSpace(a.Context.OperatingSystem) || string.IsNullOrWhiteSpace(b.Context.OperatingSystem) || string.IsNullOrWhiteSpace(a.Context.Architecture) || string.IsNullOrWhiteSpace(b.Context.Architecture) || a.Context.Runtime != b.Context.Runtime || a.Context.OperatingSystem != b.Context.OperatingSystem || a.Context.Architecture != b.Context.Architecture) yield return "Runtime, OS, or architecture differs or is unavailable.";
                if (a.Context.RequestDnsSec != b.Context.RequestDnsSec || a.Context.ValidateDnsSec != b.Context.ValidateDnsSec || a.Context.BootstrapEndpoint != b.Context.BootstrapEndpoint || a.Context.ForceDohWirePost != b.Context.ForceDohWirePost || a.Context.RequestNsid != b.Context.RequestNsid || a.Context.MaxRetries != b.Context.MaxRetries || a.Context.RetryDelayMs != b.Context.RetryDelayMs || a.Context.PortOverride != b.Context.PortOverride) yield return "DNSSEC, bootstrap, or query execution settings differ.";
                if (a.Context.DnsSecTrustAnchorIdentity != b.Context.DnsSecTrustAnchorIdentity ||
                    (a.Context.ValidateDnsSec || b.Context.ValidateDnsSec) &&
                    (a.Context.DnsSecTrustAnchorIdentity == null || b.Context.DnsSecTrustAnchorIdentity == null)) {
                    yield return "Private DNSSEC trust anchors differ or their identity is unavailable.";
                }
            }
            ResolverScoreSummary x = a.Summary, y = b.Summary;
            if (x.Domains.Length == 0 || y.Domains.Length == 0 || x.RecordTypes.Length == 0 || y.RecordTypes.Length == 0) yield return "Query catalog is unavailable.";
            if (x.Mode != y.Mode || x.TimeoutMs != y.TimeoutMs || x.AttemptsPerCombination != y.AttemptsPerCombination || x.MaxConcurrency != y.MaxConcurrency || x.ConnectionMode != y.ConnectionMode) yield return "Workflow or measurement settings differ.";
            if (!SetEqual(x.Domains.Select(Canonical), y.Domains.Select(Canonical)) || !SetEqual(x.RecordTypes.Select(t => t.ToString()), y.RecordTypes.Select(t => t.ToString()))) yield return "Query catalog differs.";
            if (!SetEqual(a.Results.Select(r => ResolverCandidateIdentity.GroupKey(r.CandidateId, r.Target).ToLowerInvariant()), b.Results.Select(r => ResolverCandidateIdentity.GroupKey(r.CandidateId, r.Target).ToLowerInvariant()))) yield return "Resolver target catalog differs.";
            else if (a.Results.Any(r => b.Results.Any(s => string.Equals(ResolverCandidateIdentity.GroupKey(r.CandidateId, r.Target), ResolverCandidateIdentity.GroupKey(s.CandidateId, s.Target), StringComparison.OrdinalIgnoreCase) && (r.Resolver != s.Resolver || r.Transport != s.Transport || r.ConnectionMode != s.ConnectionMode)))) yield return "Observed resolver, transport, or connection mode differs.";
            if (x.RequiredMinSuccessCount != y.RequiredMinSuccessCount || x.RequiredMinSuccessPercent != y.RequiredMinSuccessPercent || x.RequiredMinSuccessfulCandidates != y.RequiredMinSuccessfulCandidates || x.RequiredMinConsensusPercent != y.RequiredMinConsensusPercent || x.RequireConsensus != y.RequireConsensus) yield return "Health policy thresholds differ.";
        }
        private static bool HasUnavailableIdentity(ResolverScoreRunContext? context, ResolverScoreEntry row) => context?.TargetExecutionKeys != null && !context.TryGetExecutionKey(string.IsNullOrEmpty(row.CandidateId) ? row.Target : row.CandidateId, out _);
        private static bool TryGetExecutionKeys(ResolverScoreRunContext context, ResolverScoreEntry[] results, out Dictionary<string, string> keys) {
            keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (context.TargetExecutionKeys == null) return false;
            foreach (var entry in context.TargetExecutionKeys) {
                if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value) || keys.ContainsKey(entry.Key)) return false;
                keys.Add(entry.Key, entry.Value);
            }
            foreach (ResolverScoreEntry result in results) {
                string lookup = string.IsNullOrEmpty(result.CandidateId) ? result.Target : result.CandidateId;
                if (!keys.TryGetValue(lookup, out string? executionKey) ||
                    !string.IsNullOrEmpty(result.CandidateId) && result.CandidateId != ResolverCandidateIdentity.FromExecutionKey(executionKey)) {
                    return false;
                }
            }
            return results.Length > 0 && keys.Count == results.Length;
        }
        private static bool SetEqual(IEnumerable<string> a, IEnumerable<string> b) => new HashSet<string>(a, StringComparer.Ordinal).SetEquals(b);
    }
}
