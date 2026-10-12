using System.Collections.Generic;

namespace DnsClientX {
    // Binds saved execution metadata and keeps report/evaluation/snapshot recommendation flags consistent.
    internal static class ResolverScoreRecommendationBinding {
        internal static void Apply(ResolverProbeReport report, ResolverQueryRunOptions options, IReadOnlyList<ResolverExecutionTarget> targets) {
            if (!Bind(report.Snapshot, options, targets)) return;
            report.Summary.RecommendationAvailable = report.Evaluation.RecommendationAvailable = false;
            report.Summary.RecommendedTarget = report.Evaluation.RecommendedTarget = "none";
            report.Summary.RecommendedResolver = report.Evaluation.RecommendedResolver = "none";
            report.Summary.RecommendedTransport = report.Evaluation.RecommendedTransport = "none";
            report.Summary.RecommendedAverageMs = report.Evaluation.RecommendedAverageMs = 0;
            report.Summary.RecommendationSource = report.Evaluation.RecommendationSource = "none";
            report.Summary.RecommendationStatus = report.Evaluation.RecommendationStatus = report.Snapshot.Summary.RecommendationStatus;
            report.Summary.RecommendationReason = report.Evaluation.RecommendationReason = report.Snapshot.Summary.RecommendationReason;
            foreach (var row in report.Results) row.IsRecommended = false;
            foreach (var row in report.Evaluation.Results) row.IsRecommended = false;
        }

        internal static void Apply(ResolverBenchmarkReport report, ResolverQueryRunOptions options, IReadOnlyList<ResolverExecutionTarget> targets) {
            if (!Bind(report.Snapshot, options, targets)) return;
            report.Summary.RecommendationAvailable = report.Evaluation.RecommendationAvailable = false;
            report.Summary.RecommendedTarget = report.Evaluation.RecommendedTarget = "none";
            report.Summary.RecommendedResolver = report.Evaluation.RecommendedResolver = "none";
            report.Summary.RecommendedTransport = report.Evaluation.RecommendedTransport = "none";
            report.Summary.RecommendedAverageMs = report.Evaluation.RecommendedAverageMs = 0;
            foreach (var row in report.Results) row.IsRecommended = false;
            foreach (var row in report.Evaluation.Results) row.IsRecommended = false;
        }

        private static bool Bind(ResolverScoreSnapshot snapshot, ResolverQueryRunOptions options, IReadOnlyList<ResolverExecutionTarget> targets) {
            snapshot.Context = ResolverScoreRunContext.Capture(options, targets);
            ResolverScoreSummary summary = snapshot.Summary;
            if (!summary.RecommendationAvailable || snapshot.Context.TryGetExecutionKey(summary.RecommendedTarget, out _)) return false;
            summary.RecommendationAvailable = false;
            summary.RecommendedTarget = summary.RecommendedResolver = summary.RecommendedTransport = "none";
            summary.RecommendedAverageMs = 0;
            summary.RecommendationSource = "none";
            summary.RecommendationStatus = "unavailable";
            summary.RecommendationReason = "ambiguous candidate label identifies multiple resolver execution configurations";
            foreach (var row in snapshot.Results) row.IsRecommended = false;
            return true;
        }
    }
}
