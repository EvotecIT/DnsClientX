using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Coordinates end-to-end resolver probe workflows from normalized targets to shared reports.
    /// </summary>
    public static class ResolverProbeWorkflow {
        /// <summary>
        /// Resolves probe targets from a shared target source and builds the shared report for the completed attempts.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="name">The DNS name to query.</param>
        /// <param name="recordType">The DNS record type to query.</param>
        /// <param name="timeoutMs">The effective timeout used for the report summary.</param>
        /// <param name="runOptions">Execution settings for the probe run.</param>
        /// <param name="policy">The policy gates applied to the completed probe attempts.</param>
        /// <param name="progress">Optional callback invoked as probe attempts complete.</param>
        /// <param name="builtInOverride">Optional built-in execution override used by tests or adapters.</param>
        /// <param name="explicitOverride">Optional explicit-endpoint execution override used by tests or adapters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed probe report.</returns>
        public static async Task<ResolverProbeReport> RunAsync(
            ResolverExecutionTargetSource targetSource,
            string name,
            DnsRecordType recordType,
            int timeoutMs,
            ResolverQueryRunOptions runOptions,
            ResolverProbePolicy policy,
            Action<int, int>? progress = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await RunAsync(
                targets,
                name,
                recordType,
                timeoutMs,
                runOptions,
                policy,
                progress,
                builtInOverride,
                explicitOverride,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a probe workflow and builds the shared report for the completed attempts.
        /// </summary>
        /// <param name="targets">The normalized resolver targets to probe.</param>
        /// <param name="name">The DNS name to query.</param>
        /// <param name="recordType">The DNS record type to query.</param>
        /// <param name="timeoutMs">The effective timeout used for the report summary.</param>
        /// <param name="runOptions">Execution settings for the probe run.</param>
        /// <param name="policy">The policy gates applied to the completed probe attempts.</param>
        /// <param name="progress">Optional callback invoked as probe attempts complete.</param>
        /// <param name="builtInOverride">Optional built-in execution override used by tests or adapters.</param>
        /// <param name="explicitOverride">Optional explicit-endpoint execution override used by tests or adapters.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed probe report.</returns>
        public static async Task<ResolverProbeReport> RunAsync(
            IReadOnlyList<ResolverExecutionTarget> targets,
            string name,
            DnsRecordType recordType,
            int timeoutMs,
            ResolverQueryRunOptions runOptions,
            ResolverProbePolicy policy,
            Action<int, int>? progress = null,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride = null,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride = null,
            CancellationToken cancellationToken = default) {
            ResolverQueryAttemptResult[] attempts = await ResolverProbeRunner.RunAsync(
                targets,
                name,
                recordType,
                runOptions,
                progress,
                builtInOverride,
                explicitOverride,
                cancellationToken).ConfigureAwait(false);

            return ResolverProbeReportBuilder.Build(
                attempts,
                name,
                recordType,
                timeoutMs,
                policy);
        }
    }
}
