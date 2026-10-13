using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

public partial class ClientX {
    /// <summary>Sends all operations and prerequisites in one atomic RFC 2136 UPDATE message.</summary>
    /// <remarks>The authoritative server evaluates prerequisites before applying operations. This API does not
    /// split, retry or partially replay a batch. It requires an explicit authoritative UDP/TCP configuration.</remarks>
    public async Task<DnsResponse> UpdateRecordsAsync(DnsUpdateBatch batch, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (batch == null) { throw new ArgumentNullException(nameof(batch)); }
        cancellationToken.ThrowIfCancellationRequested();
        Configuration configuration = EndpointConfiguration.CreateQuerySnapshot();
        EnsureWireUpdateTarget(configuration);
        DnsUpdateRequestMessage message = DnsUpdateMessage.CreateBatchMessage(batch, configuration.TsigKey);
        DnsResponse response = await DnsWireUpdateTcp.SendUpdateAsync(message, Debug, configuration, cancellationToken).ConfigureAwait(false);
        if (response.Status != DnsResponseCode.NoError) { throw new DnsClientException($"DNS update failed with {response.Status}", response); }
        return response;
    }
}
