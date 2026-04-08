using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Coordinates shared single-target query and update execution for adapter-facing workflows.
    /// </summary>
    public static class ResolverSingleOperationWorkflow {
        /// <summary>
        /// Resolves a single target source and executes a DNS query.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="name">The DNS name to query.</param>
        /// <param name="recordType">The DNS record type to query.</param>
        /// <param name="requestDnsSec">Whether DNSSEC data should be requested.</param>
        /// <param name="validateDnsSec">Whether DNSSEC data should be validated.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed operation result.</returns>
        public static async Task<ResolverSingleOperationResult> QueryAsync(
            ResolverExecutionTargetSource targetSource,
            string name,
            DnsRecordType recordType,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await QueryAsync(target, name, recordType, requestDnsSec, validateDnsSec, clientOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a DNS query against one normalized execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="name">The DNS name to query.</param>
        /// <param name="recordType">The DNS record type to query.</param>
        /// <param name="requestDnsSec">Whether DNSSEC data should be requested.</param>
        /// <param name="validateDnsSec">Whether DNSSEC data should be validated.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed operation result.</returns>
        public static async Task<ResolverSingleOperationResult> QueryAsync(
            ResolverExecutionTarget target,
            string name,
            DnsRecordType recordType,
            bool requestDnsSec = false,
            bool validateDnsSec = false,
            ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, clientOptions);
            var stopwatch = Stopwatch.StartNew();
            DnsResponse response = await client.Resolve(name, recordType, requestDnsSec, validateDnsSec, cancellationToken: cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return CreateResult(client, response, stopwatch.Elapsed);
        }

        /// <summary>
        /// Resolves a single target source and executes a DNS update.
        /// </summary>
        /// <param name="targetSource">The shared target source to resolve.</param>
        /// <param name="zone">The zone to update.</param>
        /// <param name="name">The record name to add or modify.</param>
        /// <param name="recordType">The record type to add or modify.</param>
        /// <param name="data">The record data.</param>
        /// <param name="ttl">The record TTL.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed operation result.</returns>
        public static async Task<ResolverSingleOperationResult> UpdateAsync(
            ResolverExecutionTargetSource targetSource,
            string zone,
            string name,
            DnsRecordType recordType,
            string data,
            int ttl = 300,
            ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(targetSource, cancellationToken).ConfigureAwait(false);
            return await UpdateAsync(target, zone, name, recordType, data, ttl, clientOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a DNS update against one normalized execution target.
        /// </summary>
        /// <param name="target">The normalized execution target.</param>
        /// <param name="zone">The zone to update.</param>
        /// <param name="name">The record name to add or modify.</param>
        /// <param name="recordType">The record type to add or modify.</param>
        /// <param name="data">The record data.</param>
        /// <param name="ttl">The record TTL.</param>
        /// <param name="clientOptions">Optional shared client creation options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The completed operation result.</returns>
        public static async Task<ResolverSingleOperationResult> UpdateAsync(
            ResolverExecutionTarget target,
            string zone,
            string name,
            DnsRecordType recordType,
            string data,
            int ttl = 300,
            ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, clientOptions);
            var stopwatch = Stopwatch.StartNew();
            DnsResponse response = await client.UpdateRecordAsync(zone, name, recordType, data, ttl, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return CreateResult(client, response, stopwatch.Elapsed);
        }

        private static ResolverSingleOperationResult CreateResult(ClientX client, DnsResponse response, System.TimeSpan elapsed) {
            return new ResolverSingleOperationResult {
                Response = response,
                Elapsed = elapsed,
                SelectionStrategy = client.EndpointConfiguration.SelectionStrategy,
                RequestFormat = client.EndpointConfiguration.RequestFormat,
                CacheEnabled = client.CacheEnabled,
                AuditTrail = client.AuditTrail as AuditEntry[] ?? client.AuditTrail.ToArray(),
                ConfiguredResolverHost = client.EndpointConfiguration.BaseUri?.Host ?? client.EndpointConfiguration.Hostname,
                ConfiguredResolverPort = client.EndpointConfiguration.BaseUri?.Port ?? client.EndpointConfiguration.Port
            };
        }
    }
}
