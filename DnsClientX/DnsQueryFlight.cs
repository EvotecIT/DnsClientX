using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX;

/// <summary>A shared query owns its cancellation until the final waiter has left.</summary>
internal sealed class DnsQueryFlight<T> {
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Lazy<Task<T>> _task;
    private int _waiters;
    private bool _closed;

    private DnsQueryFlight(Func<CancellationToken, Task<T>> operation) {
        _task = new Lazy<Task<T>>(() => RunAsync(operation), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private Task<T> Task => _task.Value;

    internal static async Task<(T Result, bool OwnsFlight)> JoinAsync(
        ConcurrentDictionary<string, DnsQueryFlight<T>> flights, string key,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        DnsQueryFlight<T> flight;
        bool ownsFlight;
        while (true) {
            var candidate = new DnsQueryFlight<T>(operation);
            flight = flights.GetOrAdd(key, candidate);
            ownsFlight = ReferenceEquals(candidate, flight);
            if (!ownsFlight) {
                candidate.DiscardUnused();
            }
            if (flight.TryAcquire()) {
                break;
            }
            Remove(flights, key, flight);
        }
        try {
            var task = flight.Task;
            if (ownsFlight) {
                _ = RemoveWhenCompletedAsync(flights, key, flight, task);
            }
            if (cancellationToken.CanBeCanceled && !task.IsCompleted) {
                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
                if (await System.Threading.Tasks.Task.WhenAny(task, canceled.Task).ConfigureAwait(false) != task) {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            return (await task.ConfigureAwait(false), ownsFlight);
        } finally {
            if (await flight.ReleaseAsync().ConfigureAwait(false)) {
                Remove(flights, key, flight);
            }
        }
    }

    private static async Task RemoveWhenCompletedAsync(ConcurrentDictionary<string, DnsQueryFlight<T>> flights,
        string key, DnsQueryFlight<T> flight, Task<T> task) {
        try { await task.ConfigureAwait(false); }
        catch { /* Every waiter observes the original result or failure. */ }
        finally { Remove(flights, key, flight); }
    }

    private static void Remove(ConcurrentDictionary<string, DnsQueryFlight<T>> flights, string key, DnsQueryFlight<T> flight) =>
        ((ICollection<KeyValuePair<string, DnsQueryFlight<T>>>)flights).Remove(new(key, flight));

    private bool TryAcquire() {
        lock (_gate) {
            if (_closed) return false;
            _waiters++;
            return true;
        }
    }

    // Only a candidate never admitted to the dictionary can be discarded this way.
    private void DiscardUnused() => _cancellation.Dispose();

    private async Task<bool> ReleaseAsync() {
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

    private async Task<T> RunAsync(Func<CancellationToken, Task<T>> operation) =>
        await operation(_cancellation.Token).ConfigureAwait(false);
}
