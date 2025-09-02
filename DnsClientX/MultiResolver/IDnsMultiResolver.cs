using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Provides a resolver interface that can query multiple endpoints based on a selection strategy.
    /// </summary>
    public interface IDnsMultiResolver {
        /// <summary>
        /// Queries a single name for the specified record type.
        /// </summary>
        Task<DnsResponse> QueryAsync(string name, DnsRecordType type, CancellationToken ct = default);

        /// <summary>
        /// Queries multiple names of the same record type, preserving input order.
        /// </summary>
        Task<DnsResponse[]> QueryBatchAsync(string[] names, DnsRecordType type, CancellationToken ct = default);
    }
}
