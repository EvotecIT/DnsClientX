using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Coordinates end-to-end resolver benchmark workflows from normalized targets to shared reports.
    /// </summary>
    public static class ResolverBenchmarkWorkflow {
        /// <summary>
        /// Resolves benchmark targets from a shared target source and builds the shared report for the completed attempts.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="names">The DNS names to query.</param>
        /// <param name="recordTypes">The DNS record types to query.</param>
        /// <param name="attemptsPerCombination">The number of attempts per target/name/type combination.</param>
        /// <param name="maxConcurrency">The maximum number of in-flight attempts.</param>
        /// <param name="timeoutMs">The effective timeout used for the report summary.</param>
        /// <param name="runOptions">Execution settings for the benchmark run.</param>
        /// <param name="policy">The policy gates applied to the completed benchmark attempts.</param>
        /// <param name="progress">Optional callback invoked as benchmark attempts complete.</param>
        /// <param name="builtInOverride">Optional built-in execution override used by tests or adapters.</param>
        /// <param name="explicitOverride">Optional explicit-endpoint execution override used by tests or adapters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed benchmark report.</returns>
        public static async Task<ResolverBenchmarkReport> RunAsync(
            ResolverExecutionTargetSource targetSource,
            IReadOnlyList<string> names,
            IReadOnlyList<DnsRecordType> recordTypes,
            int attemptsPerCombination,
            int maxConcurrency,
            int timeoutMs,
            ResolverQueryRunOptions runOptions,
            ResolverBenchmarkPolicy policy,
            Action<int, int>? progress = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await RunAsync(
                targets,
                names,
                recordTypes,
                attemptsPerCombination,
                maxConcurrency,
                timeoutMs,
                runOptions,
                policy,
                progress,
                builtInOverride,
                explicitOverride,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a benchmark workflow and builds the shared report for the completed attempts.
        /// </summary>
        /// <param name="targets">The normalized resolver targets to benchmark.</param>
        /// <param name="names">The DNS names to query.</param>
        /// <param name="recordTypes">The DNS record types to query.</param>
        /// <param name="attemptsPerCombination">The number of attempts per target/name/type combination.</param>
        /// <param name="maxConcurrency">The maximum number of in-flight attempts.</param>
        /// <param name="timeoutMs">The effective timeout used for the report summary.</param>
        /// <param name="runOptions">Execution settings for the benchmark run.</param>
        /// <param name="policy">The policy gates applied to the completed benchmark attempts.</param>
        /// <param name="progress">Optional callback invoked as benchmark attempts complete.</param>
        /// <param name="builtInOverride">Optional built-in execution override used by tests or adapters.</param>
        /// <param name="explicitOverride">Optional explicit-endpoint execution override used by tests or adapters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed benchmark report.</returns>
        public static async Task<ResolverBenchmarkReport> RunAsync(
            IReadOnlyList<ResolverExecutionTarget> targets,
            IReadOnlyList<string> names,
            IReadOnlyList<DnsRecordType> recordTypes,
            int attemptsPerCombination,
            int maxConcurrency,
            int timeoutMs,
            ResolverQueryRunOptions runOptions,
            ResolverBenchmarkPolicy policy,
            Action<int, int>? progress = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            ResolverQueryAttemptResult[] attempts = await ResolverBenchmarkRunner.RunAsync(
                targets,
                names,
                recordTypes,
                attemptsPerCombination,
                maxConcurrency,
                runOptions,
                progress,
                builtInOverride,
                explicitOverride,
                cancellationToken).ConfigureAwait(false);

            return ResolverBenchmarkReportBuilder.Build(
                attempts,
                names as string[] ?? new List<string>(names).ToArray(),
                recordTypes as DnsRecordType[] ?? new List<DnsRecordType>(recordTypes).ToArray(),
                attemptsPerCombination,
                maxConcurrency,
                timeoutMs,
                policy);
        }
    }
}
