using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Coordinates shared single-target recursive AXFR workflows.
    /// </summary>
    public static class ResolverZoneTransferWorkflow {
        /// <summary>
        /// Resolves a single target source and runs recursive AXFR discovery and transfer.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="zone">The zone to transfer.</param>
        /// <param name="port">The TCP port to use for AXFR.</param>
        /// <param name="retryOnTransient">Whether to retry individual AXFR attempts on transient failures.</param>
        /// <param name="maxRetries">Maximum retry attempts per authoritative target.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between AXFR retries.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed recursive zone transfer result.</returns>
        public static async Task<RecursiveZoneTransferResult> RunRecursiveAsync(
            ResolverExecutionTargetSource targetSource,
            string zone,
            int port = 53,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await RunRecursiveAsync(target, zone, port, retryOnTransient, maxRetries, retryDelayMs, clientOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs recursive AXFR discovery and transfer against one normalized execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="zone">The zone to transfer.</param>
        /// <param name="port">The TCP port to use for AXFR.</param>
        /// <param name="retryOnTransient">Whether to retry individual AXFR attempts on transient failures.</param>
        /// <param name="maxRetries">Maximum retry attempts per authoritative target.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between AXFR retries.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed recursive zone transfer result.</returns>
        public static async Task<RecursiveZoneTransferResult> RunRecursiveAsync(
            ResolverExecutionTarget target,
            string zone,
            int port = 53,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, clientOptions);
            return await client.ZoneTransferRecursiveAsync(zone, port, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a single target source and streams a direct AXFR from that target.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="zone">The zone to transfer.</param>
        /// <param name="retryOnTransient">Whether to retry the AXFR on transient failures.</param>
        /// <param name="maxRetries">Maximum retry attempts.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The streamed AXFR records.</returns>
        public static async IAsyncEnumerable<ZoneTransferResult> StreamAsync(
            ResolverExecutionTargetSource targetSource,
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            ResolverExecutionClientOptions? clientOptions = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(targetSource, cancellationToken).ConfigureAwait(false);
            await foreach (ZoneTransferResult recordSet in StreamAsync(target, zone, retryOnTransient, maxRetries, retryDelayMs, clientOptions, cancellationToken).ConfigureAwait(false)) {
                yield return recordSet;
            }
        }

        /// <summary>
        /// Streams a direct AXFR from one normalized execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="zone">The zone to transfer.</param>
        /// <param name="retryOnTransient">Whether to retry the AXFR on transient failures.</param>
        /// <param name="maxRetries">Maximum retry attempts.</param>
        /// <param name="retryDelayMs">Base delay in milliseconds between retries.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The streamed AXFR records.</returns>
        public static async IAsyncEnumerable<ZoneTransferResult> StreamAsync(
            ResolverExecutionTarget target,
            string zone,
            bool retryOnTransient = true,
            int maxRetries = 3,
            int retryDelayMs = 100,
            ResolverExecutionClientOptions? clientOptions = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, clientOptions);
            await foreach (ZoneTransferResult recordSet in client.ZoneTransferStreamAsync(zone, retryOnTransient, maxRetries, retryDelayMs, cancellationToken).ConfigureAwait(false)) {
                yield return recordSet;
            }
        }
    }
}
