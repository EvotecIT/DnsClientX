using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Coordinates shared single-target DNS update and delete workflows.
    /// </summary>
    public static class ResolverUpdateWorkflow {
        /// <summary>
        /// Resolves a single target source and sends a DNS update request.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="zone">The zone to update.</param>
        /// <param name="name">The record name to add or modify.</param>
        /// <param name="type">The record type to add or modify.</param>
        /// <param name="data">The record data.</param>
        /// <param name="ttl">The record TTL.</param>
        /// <param name="portOverride">Optional port override applied after client creation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The update response.</returns>
        public static async Task<DnsResponse> UpdateAsync(
            ResolverExecutionTargetSource targetSource,
            string zone,
            string name,
            DnsRecordType type,
            string data,
            int ttl = 300,
            int? portOverride = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await UpdateAsync(target, zone, name, type, data, ttl, portOverride, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a DNS update request against one normalized execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="zone">The zone to update.</param>
        /// <param name="name">The record name to add or modify.</param>
        /// <param name="type">The record type to add or modify.</param>
        /// <param name="data">The record data.</param>
        /// <param name="ttl">The record TTL.</param>
        /// <param name="portOverride">Optional port override applied after client creation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The update response.</returns>
        public static async Task<DnsResponse> UpdateAsync(
            ResolverExecutionTarget target,
            string zone,
            string name,
            DnsRecordType type,
            string data,
            int ttl = 300,
            int? portOverride = null,
            CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, portOverride);
            return await client.UpdateRecordAsync(zone, name, type, data, ttl, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resolves a single target source and sends a DNS delete request.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="zone">The zone containing the record.</param>
        /// <param name="name">The record name to remove.</param>
        /// <param name="type">The record type to remove.</param>
        /// <param name="portOverride">Optional port override applied after client creation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The delete response.</returns>
        public static async Task<DnsResponse> DeleteAsync(
            ResolverExecutionTargetSource targetSource,
            string zone,
            string name,
            DnsRecordType type,
            int? portOverride = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await DeleteAsync(target, zone, name, type, portOverride, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a DNS delete request against one normalized execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="zone">The zone containing the record.</param>
        /// <param name="name">The record name to remove.</param>
        /// <param name="type">The record type to remove.</param>
        /// <param name="portOverride">Optional port override applied after client creation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The delete response.</returns>
        public static async Task<DnsResponse> DeleteAsync(
            ResolverExecutionTarget target,
            string zone,
            string name,
            DnsRecordType type,
            int? portOverride = null,
            CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, portOverride);
            return await client.DeleteRecordAsync(zone, name, type, cancellationToken).ConfigureAwait(false);
        }
    }
}
