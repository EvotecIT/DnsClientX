#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Net.Quic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    #pragma warning disable CA2252, CA1416
    internal sealed class DnsQuicConnectionPool : IAsyncDisposable {
        private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

        internal async ValueTask<QuicConnection> GetAsync(string key, QuicClientConnectionOptions options,
            Func<QuicClientConnectionOptions, CancellationToken, ValueTask<QuicConnection>> factory,
            CancellationToken cancellationToken) {
            Entry entry = _entries.GetOrAdd(key, _ => new Entry());
            await entry.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                if (entry.Connection != null) return entry.Connection;
                entry.Connection = await factory(options, cancellationToken).ConfigureAwait(false);
                return entry.Connection;
            } finally {
                entry.Gate.Release();
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

        public async ValueTask DisposeAsync() {
            foreach (Entry entry in _entries.Values) {
                await entry.Gate.WaitAsync().ConfigureAwait(false);
                try {
                    if (entry.Connection != null) {
                        await entry.Connection.DisposeAsync().ConfigureAwait(false);
                        entry.Connection = null;
                    }
                } finally {
                    entry.Gate.Release();
                    entry.Gate.Dispose();
                }
            }
            _entries.Clear();
        }

        private sealed class Entry {
            internal SemaphoreSlim Gate { get; } = new(1, 1);
            internal QuicConnection? Connection { get; set; }
        }
    }
    #pragma warning restore CA2252, CA1416
}
#endif
