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
            if (targets == null) { throw new ArgumentNullException(nameof(targets)); }
            if (names == null) { throw new ArgumentNullException(nameof(names)); }
            if (recordTypes == null) { throw new ArgumentNullException(nameof(recordTypes)); }
            if (options == null) { throw new ArgumentNullException(nameof(options)); }
            if (attemptsPerCombination < 1) { throw new ArgumentOutOfRangeException(nameof(attemptsPerCombination)); }
            if (maxConcurrency < 1) { throw new ArgumentOutOfRangeException(nameof(maxConcurrency)); }
            if (options.ConnectionMode != ResolverQueryConnectionMode.Cold && options.ConnectionMode != ResolverQueryConnectionMode.Warm) {
                throw new ArgumentOutOfRangeException(nameof(options.ConnectionMode));
            }
            cancellationToken.ThrowIfCancellationRequested();
            int totalQueries;
            try {
                totalQueries = checked(targets.Count * names.Count * recordTypes.Count * attemptsPerCombination);
            } catch (OverflowException) {
                throw new ArgumentException("The requested benchmark exceeds the supported number of attempts.");
            }
            if (totalQueries == 0) { return Array.Empty<ResolverQueryAttemptResult>(); }
            ResolverExecutionTarget[] targetList = targets.ToArray();
            string[] nameList = names.ToArray();
            DnsRecordType[] typeList = recordTypes.ToArray();

            var results = new ResolverQueryAttemptResult[totalQueries];
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            ResolverQueryClientSession? clients = options.ConnectionMode == ResolverQueryConnectionMode.Warm
                ? new ResolverQueryClientSession(targetList, options) : null;
            int next = -1;
            int completed = 0;
            var progressGate = new object();
            System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
            try {
                // Only the worker count creates tasks; output order follows the input matrix,
                // regardless of completion order. A failing callback cancels and drains peers.
                var workers = new Task[Math.Min(maxConcurrency, totalQueries)];
                for (int worker = 0; worker < workers.Length; worker++) {
                    workers[worker] = Task.Run(async () => {
                        try {
                            while (true) {
                                stop.Token.ThrowIfCancellationRequested();
                                int index = Interlocked.Increment(ref next);
                                if (index >= totalQueries) { return; }
                                int combination = index / attemptsPerCombination;
                                int typeIndex = combination % typeList.Length;
                                combination /= typeList.Length;
                                int nameIndex = combination % nameList.Length;
                                int targetIndex = combination / nameList.Length;
                                results[index] = await ResolverQueryExecutor.ExecuteAsync(
                                    targetList[targetIndex], nameList[nameIndex], typeList[typeIndex], options,
                                    builtInOverride, explicitOverride, stop.Token,
                                    clients == null ? null : () => clients.GetClient(targetIndex)).ConfigureAwait(false);
                                if (results[index].ConnectionMode != options.ConnectionMode) {
                                    results[index] = results[index].WithConnectionMode(options.ConnectionMode);
                                }
                                lock (progressGate) {
                                    stop.Token.ThrowIfCancellationRequested();
                                    progress?.Invoke(++completed, totalQueries);
                                }
                            }
                        } catch (Exception ex) {
                            lock (progressGate) {
                                failure ??= System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
                            }
                            stop.Cancel();
                        }
                    });
                }
                await Task.WhenAll(workers).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                failure?.Throw();
                return results;
            } finally {
                if (clients != null) { await clients.DisposeAsync().ConfigureAwait(false); }
            }
        }
    }
}
