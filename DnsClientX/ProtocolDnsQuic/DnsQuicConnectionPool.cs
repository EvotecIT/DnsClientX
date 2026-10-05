#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Quic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    #pragma warning disable CA2252, CA1416
    internal sealed class DnsQuicConnectionPool : IAsyncDisposable {
        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lifetimeGate = new();
        private readonly CancellationTokenSource _lifetime = new();
        private bool _closed;
        private Task? _closing;

        internal async ValueTask<QuicConnection> GetAsync(string key, QuicClientConnectionOptions options,
            Func<QuicClientConnectionOptions, CancellationToken, ValueTask<QuicConnection>> factory,
            CancellationToken cancellationToken) {
            Entry entry;
            CancellationTokenSource deadline;
            lock (_lifetimeGate) {
                ThrowIfClosed();
                deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
                entry = _entries.GetOrAdd(key, _ => new Entry());
            }
            using (deadline) {
                bool admitted = false;
                try {
                    await entry.Gate.WaitAsync(deadline.Token).ConfigureAwait(false);
                    admitted = true;
                    lock (_lifetimeGate) {
                        ThrowIfClosed();
                        if (entry.Connection != null) return entry.Connection;
                    }
                    QuicConnection created = await factory(options, deadline.Token).ConfigureAwait(false);
                    lock (_lifetimeGate) {
                        if (!_closed) {
                            entry.Connection = created;
                            return created;
                        }
                    }
                    // Shutdown won publication, even if the platform factory completed after cancellation.
                    await created.DisposeAsync().ConfigureAwait(false);
                    throw new ObjectDisposedException(nameof(DnsQuicConnectionPool));
                } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                    lock (_lifetimeGate) { ThrowIfClosed(); }
                    throw;
                } finally {
                    if (admitted) entry.Gate.Release();
                }
            }
        }

        internal async ValueTask InvalidateAsync(string key, QuicConnection connection,
            Func<QuicConnection, ValueTask>? beforeDispose = null) {
            if (!_entries.TryGetValue(key, out Entry? entry)) return;
            await entry.Gate.WaitAsync().ConfigureAwait(false);
            try {
                if (!ReferenceEquals(entry.Connection, connection)) return;
                entry.Connection = null;
                try {
                    if (beforeDispose != null) await beforeDispose(connection).ConfigureAwait(false);
                } finally {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
            } finally {
                entry.Gate.Release();
            }
        }

        public ValueTask DisposeAsync() {
            TaskCompletionSource<bool> completion;
            Entry[] entries;
            lock (_lifetimeGate) {
                if (_closing != null) return new ValueTask(_closing);
                _closed = true;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _closing = completion.Task;
                entries = _entries.Values.ToArray();
            }
            _ = CloseEntriesAsync(entries, completion);
            return new ValueTask(completion.Task);
        }

        private async Task CloseEntriesAsync(Entry[] entries, TaskCompletionSource<bool> completion) {
            var failures = new List<Exception>();
            try {
                try { _lifetime.Cancel(); } catch (Exception exception) { failures.Add(exception); }
                foreach (Entry entry in entries) {
                    await entry.Gate.WaitAsync().ConfigureAwait(false);
                    try {
                        QuicConnection? connection = entry.Connection;
                        entry.Connection = null;
                        if (connection != null) await connection.DisposeAsync().ConfigureAwait(false);
                    } catch (Exception exception) {
                        failures.Add(exception);
                    } finally {
                        entry.Gate.Release();
                        // Admitted callers may still unwind or acquire this gate. Its managed
                        // wait handle is never requested, so leave final reclamation to GC.
                    }
                }
            } catch (Exception exception) {
                failures.Add(exception);
            } finally {
                _entries.Clear();
                _lifetime.Dispose();
                if (failures.Count == 0) completion.TrySetResult(true);
                else completion.TrySetException(failures);
            }
        }

        private void ThrowIfClosed() {
            if (_closed) throw new ObjectDisposedException(nameof(DnsQuicConnectionPool));
        }
        private sealed class Entry {
            internal SemaphoreSlim Gate { get; } = new(1, 1);
            internal QuicConnection? Connection { get; set; }
        }
    }
    #pragma warning restore CA2252, CA1416
}
#endif
