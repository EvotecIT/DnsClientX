using System;
using System.Collections.Generic;

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

            ResolverScoreSummary? summary = snapshot.Summary;
            if (summary == null) {
                error = "Resolver score snapshot summary is required.";
                return false;
            }

            if (!summary.RecommendationAvailable) {
                error = $"No recommendation is available in the {summary.Mode} snapshot.";
                return false;
            }

            string target = summary.RecommendedTarget?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target) || string.Equals(target, "none", StringComparison.OrdinalIgnoreCase)) {
                error = $"The {summary.Mode} snapshot does not contain a recommended target.";
                return false;
            }

            if (Enum.TryParse(target, ignoreCase: true, out DnsEndpoint builtInEndpoint) &&
                Enum.IsDefined(typeof(DnsEndpoint), builtInEndpoint) &&
                builtInEndpoint != DnsEndpoint.Custom) {
                selection = new ResolverSelectionResult {
                    Mode = summary.Mode,
                    Target = target,
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

            DnsResolverEndpoint[] endpoints = EndpointParser.TryParseMany(new[] { target }, out IReadOnlyList<string> errors);
            if (endpoints.Length == 1 && errors.Count == 0) {
                selection = new ResolverSelectionResult {
                    Mode = summary.Mode,
                    Target = target,
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
