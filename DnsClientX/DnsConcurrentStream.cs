using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>Owns bounded query streaming, consumer backpressure, and unfinished query cleanup.</summary>
internal static class DnsConcurrentStream {
    internal const int DefaultConcurrency = 32;

    /// <summary>
    /// Keeps at most the configured number of operations admitted. Disposing the enumerator
    /// cancels and observes every admitted operation before releasing its cancellation source.
    /// </summary>
    internal static async IAsyncEnumerable<TResult> Execute<TWork, TResult>(
        IEnumerable<TWork> work,
        Func<TWork, CancellationToken, Task<TResult>> operation,
        int? maxConcurrency = null,
        bool preserveOrder = false,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (work == null) throw new ArgumentNullException(nameof(work));
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        int concurrency = maxConcurrency is > 0 ? maxConcurrency.Value : DefaultConcurrency;
        using var ownedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var source = work.GetEnumerator();
        var pending = new List<Task<TResult>>();
        bool exhausted = false;
        try {
            while (true) {
                ownedCancellation.Token.ThrowIfCancellationRequested();
                while (!exhausted && pending.Count < concurrency) {
                    ownedCancellation.Token.ThrowIfCancellationRequested();
                    if (!source.MoveNext()) {
                        exhausted = true;
                        break;
                    }
                    pending.Add(operation(source.Current, ownedCancellation.Token));
                }
                if (pending.Count == 0) yield break;
                Task<TResult> finished = preserveOrder
                    ? pending[0]
                    : await Task.WhenAny(pending).ConfigureAwait(false);
                TResult result = await finished.ConfigureAwait(false);
                pending.Remove(finished);
                cancellationToken.ThrowIfCancellationRequested();
                // Refill only when the consumer requests another item. A slow consumer
                // bounds completed-but-unconsumed responses as well as live queries.
                yield return result;
            }
        } finally {
            try { ownedCancellation.Cancel(); }
            catch (AggregateException) { /* Callback failures must not skip cleanup. */ }
            try { await Task.WhenAll(pending).ConfigureAwait(false); }
            catch { /* The consumer sees the initiating failure or cancellation. */ }
        }
    }
}
