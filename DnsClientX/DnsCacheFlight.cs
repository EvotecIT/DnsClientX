using System;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>A shared query owns its cancellation until the final waiter has left.</summary>
internal sealed class DnsCacheFlight {
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Lazy<Task<DnsResponse>> _task;
    private int _waiters;
    private bool _closed;

    internal DnsCacheFlight(Func<CancellationToken, Task<DnsResponse>> operation) {
        _task = new Lazy<Task<DnsResponse>>(() => RunAsync(operation), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    internal Task<DnsResponse> Task => _task.Value;

    internal bool TryAcquire() {
        lock (_gate) {
            if (_closed) return false;
            _waiters++;
            return true;
        }
    }

    // Only a candidate never admitted to the dictionary can be discarded this way.
    internal void DiscardUnused() => _cancellation.Dispose();

    internal async Task<bool> ReleaseAsync() {
        lock (_gate) {
            if (--_waiters != 0) return false;
            _closed = true;
        }
        try {
            if (!Task.IsCompleted) {
                try { _cancellation.Cancel(); }
                catch (AggregateException) { /* Callback failures must not skip transport cleanup. */ }
            }
            try { await Task.ConfigureAwait(false); }
            catch { /* The caller already observes its response, exception or cancellation. */ }
        } finally {
            _cancellation.Dispose();
        }
        return true;
    }

    private async Task<DnsResponse> RunAsync(Func<CancellationToken, Task<DnsResponse>> operation) =>
        await operation(_cancellation.Token).ConfigureAwait(false);
}
