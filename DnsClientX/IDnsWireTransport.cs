using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>Transports a DNS wire message through an explicitly installed optional protocol provider.</summary>
/// <remarks>The caller owns the provider lifetime. Implementations must support concurrent queries,
/// observe cancellation, authenticate responses before returning plaintext, and never fall back to cleartext DNS.</remarks>
public interface IDnsWireTransport {
    /// <summary>Gets the protocol implemented by this provider.</summary>
    DnsRequestFormat RequestFormat { get; }

    /// <summary>Sends one DNS wire query and returns its authenticated DNS wire response.</summary>
    /// <param name="query">The complete serialized DNS query, without a TCP length prefix.</param>
    /// <param name="cancellationToken">Cancellation for this exchange.</param>
    /// <returns>The decrypted DNS response and the physical transport used by this exchange.</returns>
    Task<DnsWireTransportResponse> QueryAsync(byte[] query, CancellationToken cancellationToken = default);
}
