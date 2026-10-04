using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>Reads HTTP DNS bodies without letting a remote endpoint select the buffer size.</summary>
internal static class DnsHttpResponseBody {
    internal const int MaxWireBytes = ushort.MaxValue;
    internal const int MaxGrpcBytes = MaxWireBytes + 5;
    internal const int MaxJsonBytes = 1024 * 1024;
    internal const int MaxErrorPreviewBytes = 512;

    internal static CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken) {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout != Timeout.InfiniteTimeSpan) {
            deadline.CancelAfter(timeout);
        }
        return deadline;
    }

    internal static Task<byte[]> ReadBoundedAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken) =>
        ReadAsync(content, maxBytes, rejectOversize: true, cancellationToken);

    internal static Task<byte[]> ReadPrefixAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken) =>
        ReadAsync(content, maxBytes, rejectOversize: false, cancellationToken);

    private static async Task<byte[]> ReadAsync(HttpContent content, int maxBytes, bool rejectOversize,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (rejectOversize && content.Headers.ContentLength > maxBytes) {
            throw new DnsClientException($"HTTP DNS response exceeds the {maxBytes} byte limit.");
        }

#if NET5_0_OR_GREATER
        using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using Stream stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[4096];
        int remaining = maxBytes + (rejectOversize ? 1 : 0);

        // Closing the response stream aborts a blocked read on .NET Framework, where its
        // ReadAsync implementation may not observe the supplied cancellation token.
        using (cancellationToken.Register(() => {
            try {
                stream.Dispose();
            } catch (Exception) {
                // Cancellation is already requested; the in-flight read reports its failure.
            }
        })) {
            while (remaining > 0) {
                cancellationToken.ThrowIfCancellationRequested();
                int count;
                try {
                    count = await stream.ReadAsync(chunk, 0, Math.Min(chunk.Length, remaining), cancellationToken)
                        .ConfigureAwait(false);
                } catch (Exception ex) when (cancellationToken.IsCancellationRequested &&
                                             (ex is IOException || ex is WebException ||
                                              ex is ObjectDisposedException || ex is HttpRequestException)) {
                    throw new OperationCanceledException("HTTP DNS response read was canceled.", ex, cancellationToken);
                }
                if (count == 0) break;
                remaining -= count;
                if (rejectOversize && buffer.Length + count > maxBytes) {
                    throw new DnsClientException($"HTTP DNS response exceeds the {maxBytes} byte limit.");
                }
                buffer.Write(chunk, 0, count);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }
}
