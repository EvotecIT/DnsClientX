using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Executes shared resolver probe workflows against normalized execution targets.
    /// </summary>
    public static class ResolverProbeRunner {
        /// <summary>
        /// Executes one query per supplied target.
        /// </summary>
        /// <param name="targets">The normalized resolver targets to probe.</param>
        /// <param name="name">The DNS name to query.</param>
        /// <param name="recordType">The DNS record type to query.</param>
        /// <param name="options">Execution settings for the probe run.</param>
        /// <param name="progress">Optional callback invoked as attempts complete.</param>
        /// <param name="builtInOverride">Optional built-in execution override used by tests or adapters.</param>
        /// <param name="explicitOverride">Optional explicit-endpoint execution override used by tests or adapters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed probe attempts in target order.</returns>
        public static async Task<ResolverQueryAttemptResult[]> RunAsync(
            IReadOnlyList<ResolverExecutionTarget> targets,
            string name,
            DnsRecordType recordType,
            ResolverQueryRunOptions options,
            Action<int, int>? progress = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            if (targets == null) {
                throw new ArgumentNullException(nameof(targets));
            }

            var results = new List<ResolverQueryAttemptResult>(targets.Count);
            for (int i = 0; i < targets.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await ResolverQueryExecutor.ExecuteAsync(
                    targets[i],
                    name,
                    recordType,
                    options,
                    builtInOverride,
                    explicitOverride,
                    cancellationToken).ConfigureAwait(false));
                progress?.Invoke(i + 1, targets.Count);
            }

            return results.ToArray();
        }
    }
}
