using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Resolves shared target-source descriptions into normalized execution targets.
    /// </summary>
    public static class ResolverExecutionTargetResolver {
        /// <summary>
        /// Loads the recommended resolver selection from a persisted resolver score snapshot.
        /// </summary>
        /// <param name="selectionPath">The persisted resolver score snapshot path.</param>
        /// <returns>The recommended resolver selection.</returns>
        public static ResolverSelectionResult LoadRecommendedSelection(string selectionPath) {
            ResolverScoreSnapshot snapshot = ResolverScoreStore.Load(selectionPath);
            if (!ResolverScoreSelector.TrySelectRecommended(snapshot, out ResolverSelectionResult? selection, out string? error) || selection == null) {
                throw new InvalidOperationException(error ?? "No recommended resolver could be selected from the snapshot.");
            }

            return selection;
        }

        /// <summary>
        /// Resolves one shared target-source description into runnable execution targets.
        /// </summary>
        /// <param name="source">The target source to resolve.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The normalized execution targets.</returns>
        public static async Task<ResolverExecutionTarget[]> ResolveAsync(ResolverExecutionTargetSource source, CancellationToken cancellationToken = default) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            source.Validate();

            if (!string.IsNullOrWhiteSpace(source.ResolverSelectionPath)) {
                ResolverSelectionResult selection = LoadRecommendedSelection(source.ResolverSelectionPath!);
                return new[] { ResolverExecutionPlanBuilder.BuildSelectionTarget(selection) };
            }

            if (source.HasExplicitResolverInputs) {
                var (endpoints, errors) = await EndpointParser.TryParseManyAsync(
                    source.ResolverEndpoints,
                    source.ResolverEndpointFiles,
                    source.ResolverEndpointUrls,
                    cancellationToken).ConfigureAwait(false);
                if (errors.Count > 0) {
                    throw new ArgumentException(string.Join("; ", errors), nameof(source));
                }

                return ResolverExecutionPlanBuilder.BuildExplicitTargets(endpoints);
            }

            if (source.ProbeProfile.HasValue) {
                return ResolverExecutionPlanBuilder.BuildProbeTargets(source.ProbeProfile.Value);
            }

            return ResolverExecutionPlanBuilder.BuildBuiltInTargets(source.BuiltInEndpoints);
        }

        /// <summary>
        /// Resolves one shared target-source description into exactly one runnable execution target.
        /// </summary>
        /// <param name="source">The target source to resolve.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The normalized execution target.</returns>
        public static async Task<ResolverExecutionTarget> ResolveSingleAsync(ResolverExecutionTargetSource source, CancellationToken cancellationToken = default) {
            ResolverExecutionTarget[] targets = await ResolveAsync(source, cancellationToken).ConfigureAwait(false);
            if (targets.Length == 0) {
                throw new InvalidOperationException("No resolver execution targets were produced.");
            }

            if (targets.Length > 1) {
                throw new InvalidOperationException($"Resolver target source produced {targets.Length} targets but a single target was required.");
            }

            return targets[0];
        }
    }
}
