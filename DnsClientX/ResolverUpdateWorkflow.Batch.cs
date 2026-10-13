using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    public static partial class ResolverUpdateWorkflow {
        /// <summary>Applies one atomic RFC 2136 batch to an explicit authoritative endpoint.</summary>
        public static async Task<DnsResponse> UpdateBatchAsync(ResolverExecutionTarget target,
            DnsUpdateBatch batch, ResolverExecutionClientOptions? clientOptions = null,
            CancellationToken cancellationToken = default) {
            await using ClientX client = ResolverExecutionClientFactory.CreateClient(target, clientOptions);
            return await client.UpdateRecordsAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }
}
