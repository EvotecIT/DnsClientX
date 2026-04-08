using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Executes shared resolver benchmark workflows against normalized execution targets.
    /// </summary>
    public static class ResolverBenchmarkRunner {
        /// <summary>
        /// Executes benchmark attempts across the supplied targets, names, and record types.
        /// </summary>
        /// <param name="targets">The normalized resolver targets to benchmark.</param>
        /// <param name="names">The DNS names to query.</param>
        /// <param name="recordTypes">The DNS record types to query.</param>
        /// <param name="attemptsPerCombination">The number of attempts per target/name/type combination.</param>
        /// <param name="maxConcurrency">The maximum number of in-flight attempts.</param>
        /// <param name="options">Execution settings for the benchmark run.</param>
        /// <param name="progress">Optional callback invoked as attempts complete.</param>
        /// <param name="builtInOverride">Optional built-in execution override used by tests or adapters.</param>
        /// <param name="explicitOverride">Optional explicit-endpoint execution override used by tests or adapters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed benchmark attempts ordered by target index.</returns>
        public static async Task<ResolverQueryAttemptResult[]> RunAsync(
            IReadOnlyList<ResolverExecutionTarget> targets,
            IReadOnlyList<string> names,
            IReadOnlyList<DnsRecordType> recordTypes,
            int attemptsPerCombination,
            int maxConcurrency,
            ResolverQueryRunOptions options,
            Action<int, int>? progress = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            if (targets == null) {
                throw new ArgumentNullException(nameof(targets));
            }

            int totalQueries = targets.Count * names.Count * recordTypes.Count * attemptsPerCombination;
            int completed = 0;
            using var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency));
            var tasks = new List<Task<(int TargetIndex, ResolverQueryAttemptResult Result)>>(totalQueries);

            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++) {
                ResolverExecutionTarget target = targets[targetIndex];
                foreach (string name in names) {
                    foreach (DnsRecordType recordType in recordTypes) {
                        for (int attempt = 0; attempt < attemptsPerCombination; attempt++) {
                            tasks.Add(RunAttemptAsync(targetIndex, target, name, recordType, semaphore, options, builtInOverride, explicitOverride, () => {
                                int finished = Interlocked.Increment(ref completed);
                                progress?.Invoke(finished, totalQueries);
                            }, cancellationToken));
                        }
                    }
                }
            }

            return (await Task.WhenAll(tasks).ConfigureAwait(false))
                .OrderBy(item => item.TargetIndex)
                .Select(item => item.Result)
                .ToArray();
        }

        private static async Task<(int TargetIndex, ResolverQueryAttemptResult Result)> RunAttemptAsync(
            int targetIndex,
            ResolverExecutionTarget target,
            string name,
            DnsRecordType recordType,
            SemaphoreSlim semaphore,
            ResolverQueryRunOptions options,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride,
            Action onCompleted,
            CancellationToken cancellationToken) {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                ResolverQueryAttemptResult result = await ResolverQueryExecutor.ExecuteAsync(
                    target,
                    name,
                    recordType,
                    options,
                    builtInOverride,
                    explicitOverride,
                    cancellationToken).ConfigureAwait(false);
                return (targetIndex, result);
            } finally {
                onCompleted();
                semaphore.Release();
            }
        }
    }
}
