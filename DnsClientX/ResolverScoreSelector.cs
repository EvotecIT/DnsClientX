using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Selects a recommended resolver target from a persisted score snapshot.
    /// </summary>
    public static class ResolverScoreSelector {
        /// <summary>
        /// Tries to select the recommended resolver from a score snapshot.
        /// </summary>
        public static bool TrySelectRecommended(ResolverScoreSnapshot? snapshot, out ResolverSelectionResult? selection, out string? error) {
            selection = null;
            error = null;

            if (snapshot == null) {
                error = "Resolver score snapshot is required.";
                return false;
            }

            if (!ResolverScoreSnapshot.TryValidateCompatibility(snapshot, out error)) {
                return false;
            }

            ResolverScoreSummary? summary = snapshot.Summary;
            if (summary == null) {
                error = "Resolver score snapshot summary is required.";
                return false;
            }

            if (!summary.RecommendationAvailable) {
                error = $"No recommendation is available in the {summary.Mode} snapshot: {summary.RecommendationReason}.";
                return false;
            }

            string target = summary.RecommendedTarget?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target) || string.Equals(target, "none", StringComparison.OrdinalIgnoreCase)) {
                error = $"The {summary.Mode} snapshot does not contain a recommended target.";
                return false;
            }
            string candidateId = summary.RecommendedCandidateId?.Trim() ?? string.Empty;
            if (snapshot.SchemaVersion >= 3 && candidateId.Length == 0) {
                error = $"Recommended target '{target}' lacks its saved candidate identity.";
                return false;
            }
            string lookupKey = candidateId.Length == 0 ? target : candidateId;
            ResolverScoreEntry[] rows = (snapshot.Results ?? Array.Empty<ResolverScoreEntry>()).Where(row => row != null &&
                string.Equals(candidateId.Length == 0 ? row.Target : row.CandidateId, lookupKey, candidateId.Length == 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).ToArray();
            if (rows.Length > 1 || candidateId.Length > 0 && (rows.Length != 1 ||
                !string.Equals(rows[0].Target, target, StringComparison.OrdinalIgnoreCase))) {
                error = $"Recommended target '{target}' has ambiguous or inconsistent saved candidate rows.";
                return false;
            }
            if (candidateId.Length > 0 && snapshot.Context?.TargetExecutionKeys == null) {
                error = $"Recommended target '{target}' lacks its saved execution identity.";
                return false;
            }

            // Older snapshots only contain labels. New snapshots must reuse their saved definitions,
            // since labels intentionally omit TLS, family, timeout, EDNS, and request-format settings.
            DnsResolverEndpoint? savedEndpoint = null;
            if (snapshot.Context?.TargetExecutionKeys != null || snapshot.Context?.ExplicitEndpoints != null) {
                if (!snapshot.Context.TryGetExecutionKey(lookupKey, out string? executionKey, candidateId.Length > 0)) {
                    error = $"Recommended target '{target}' has an ambiguous or unavailable saved execution identity.";
                    return false;
                }
                if (candidateId.Length > 0 && candidateId != ResolverCandidateIdentity.FromExecutionKey(executionKey!)) {
                    error = $"Recommended target '{target}' has an inconsistent saved candidate identity.";
                    return false;
                }
                if (snapshot.Context.TryGetExplicitEndpoint(lookupKey, out savedEndpoint, candidateId.Length > 0)) {
                    try {
                        if (ResolverEndpointClientFactory.GetExecutionKey(savedEndpoint!) != executionKey) {
                            error = $"Recommended target '{target}' has inconsistent saved endpoint metadata.";
                            return false;
                        }
                    } catch (ArgumentException exception) {
                        error = $"Recommended target '{target}' has invalid saved endpoint metadata: {exception.Message}";
                        return false;
                    } catch (InvalidOperationException exception) {
                        error = $"Recommended target '{target}' has invalid saved endpoint metadata: {exception.Message}";
                        return false;
                    }
                } else if (snapshot.Context.ExplicitEndpoints?.Keys.Any(key => string.Equals(key, lookupKey, candidateId.Length == 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) == true ||
                    !Enum.TryParse(target, ignoreCase: true, out DnsEndpoint identifiedEndpoint) ||
                    !Enum.IsDefined(typeof(DnsEndpoint), identifiedEndpoint) || executionKey != "builtin:" + identifiedEndpoint) {
                    error = $"Recommended target '{target}' lacks an unambiguous saved endpoint definition.";
                    return false;
                }
            }

            if (savedEndpoint == null && Enum.TryParse(target, ignoreCase: true, out DnsEndpoint builtInEndpoint) &&
                Enum.IsDefined(typeof(DnsEndpoint), builtInEndpoint) &&
                builtInEndpoint != DnsEndpoint.Custom) {
                selection = new ResolverSelectionResult {
                    Mode = summary.Mode,
                    Target = target,
                    CandidateId = candidateId,
                    Kind = ResolverSelectionKind.BuiltInEndpoint,
                    BuiltInEndpoint = builtInEndpoint,
                    Resolver = summary.RecommendedResolver,
                    Transport = summary.RecommendedTransport,
                    AverageMs = summary.RecommendedAverageMs,
                    RecommendationSource = summary.RecommendationSource,
                    RecommendationStatus = summary.RecommendationStatus,
                    RecommendationReason = summary.RecommendationReason
                };
                return true;
            }

            if (string.Equals(target, DnsEndpoint.Custom.ToString(), StringComparison.OrdinalIgnoreCase)) {
                error = "Snapshots that recommend DnsEndpoint.Custom cannot be reused automatically because the custom endpoint configuration is not persisted in the snapshot.";
                return false;
            }

            IReadOnlyList<string> errors = Array.Empty<string>();
            DnsResolverEndpoint[] endpoints = savedEndpoint != null ? new[] { savedEndpoint }
                : EndpointParser.TryParseMany(new[] { target }, out errors);
            if (endpoints.Length == 1 && errors.Count == 0) {
                selection = new ResolverSelectionResult {
                    Mode = summary.Mode,
                    Target = target,
                    CandidateId = candidateId,
                    Kind = ResolverSelectionKind.ExplicitEndpoint,
                    ExplicitEndpoint = endpoints[0],
                    Resolver = summary.RecommendedResolver,
                    Transport = summary.RecommendedTransport,
                    AverageMs = summary.RecommendedAverageMs,
                    RecommendationSource = summary.RecommendationSource,
                    RecommendationStatus = summary.RecommendationStatus,
                    RecommendationReason = summary.RecommendationReason
                };
                return true;
            }

            error = errors.Count > 0
                ? $"Unable to parse recommended target '{target}': {string.Join(" | ", errors)}"
                : $"Unable to parse recommended target '{target}'.";
            return false;
        }
    }
}
