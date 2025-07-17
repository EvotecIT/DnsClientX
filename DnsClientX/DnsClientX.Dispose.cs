using System;
using System.Collections.Generic;
using System.Net.Http;

using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class implementing disposal logic.
    /// </summary>
    /// <remarks>
    /// Responsible for releasing HTTP clients and other managed resources.
    /// </remarks>
    public partial class ClientX : IDisposable, IAsyncDisposable {
        private bool _disposed;
        private volatile int _disposalCountIncremented = 0;
        private readonly HashSet<object> _disposedClients = new();
        private static int _disposalCount;
        internal static int DisposalCount {
            get => System.Threading.Interlocked.CompareExchange(ref _disposalCount, 0, 0);
            set => System.Threading.Interlocked.Exchange(ref _disposalCount, value);
        }

        private bool TryAddDisposedClient(object client) {
            lock (_lock) {
                return _disposedClients.Add(client);
            }
        }

        /// <inheritdoc/>
        public void Dispose() {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                HttpClientHandler? handlerLocal;
                List<HttpClient> clients;
                HttpClient? mainClient;

                lock (_lock) {
                    if (_disposed) {
                        return;
                    }
                    _disposed = true;

                    clients = new List<HttpClient>(_clients.Values);
                    _clients.Clear();

                    mainClient = Client;
                    handlerLocal = handler;
                    Client = null;
                    handler = null;
                }
                if (disposing) {
                    foreach (HttpClient client in clients) {
                        if (TryAddDisposedClient(client)) {
                            client.Dispose();
                        }
                    }

                    if (mainClient != null && TryAddDisposedClient(mainClient)) {
                        mainClient.Dispose();
                        if (_handlerOwnedByClient && handlerLocal != null) {
                            TryAddDisposedClient(handlerLocal);
                        }
                    }

                    if (!_handlerOwnedByClient && handlerLocal != null && TryAddDisposedClient(handlerLocal)) {
                        handlerLocal.Dispose();
                    }

                    lock (_lock) {
                        _disposedClients.Clear();
                    }
                }

                // Only increment disposal count once per instance
                if (System.Threading.Interlocked.CompareExchange(ref _disposalCountIncremented, 1, 0) == 0) {
                    System.Threading.Interlocked.Increment(ref _disposalCount);
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync() {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(disposing: false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources asynchronously when possible.
        /// </summary>
        /// <returns>A task representing the asynchronous disposal.</returns>
        protected virtual async ValueTask DisposeAsyncCore() {
#if !(NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER)
            await Task.CompletedTask;
#endif
            if (!_disposed) {
                HttpClientHandler? handlerLocal;
                List<HttpClient> clients;
                HttpClient? mainClient;

                lock (_lock) {
                    if (_disposed) {
                        return;
                    }
                    _disposed = true;

                    clients = new List<HttpClient>(_clients.Values);
                    _clients.Clear();

                    mainClient = Client;
                    handlerLocal = handler;
                    Client = null;
                    handler = null;
                }

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                foreach (HttpClient client in clients) {
                    if (TryAddDisposedClient(client)) {
                        if (client is IAsyncDisposable asyncClient) {
                            await asyncClient.DisposeAsync().ConfigureAwait(false);
                        } else {
                            client.Dispose();
                        }
                    }
                }
#else
                foreach (HttpClient client in clients) {
                    if (TryAddDisposedClient(client)) {
                        client.Dispose();
                    }
                }
#endif

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (mainClient != null && TryAddDisposedClient(mainClient)) {
                    if (mainClient is IAsyncDisposable asyncClient) {
                        await asyncClient.DisposeAsync().ConfigureAwait(false);
                    } else {
                        mainClient.Dispose();
                    }
                    if (_handlerOwnedByClient && handlerLocal != null) {
                        TryAddDisposedClient(handlerLocal);
                    }
                }
#else
                if (mainClient != null && TryAddDisposedClient(mainClient)) {
                    mainClient.Dispose();
                    if (_handlerOwnedByClient && handlerLocal != null) {
                        TryAddDisposedClient(handlerLocal);
                    }
                }
#endif

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (!_handlerOwnedByClient && handlerLocal != null && TryAddDisposedClient(handlerLocal)) {
                    if (handlerLocal is IAsyncDisposable asyncHandler) {
                        await asyncHandler.DisposeAsync().ConfigureAwait(false);
                    } else {
                        handlerLocal.Dispose();
                    }
                }
#else
                if (!_handlerOwnedByClient && handlerLocal != null && TryAddDisposedClient(handlerLocal)) {
                    handlerLocal.Dispose();
                }
#endif

                lock (_lock) {
                    _disposedClients.Clear();
                }

                _disposed = true;
                // Only increment disposal count once per instance
                if (System.Threading.Interlocked.CompareExchange(ref _disposalCountIncremented, 1, 0) == 0) {
                    System.Threading.Interlocked.Increment(ref _disposalCount);
                }
            }
        }

        /// <summary>
        /// Finalizer to ensure unmanaged resources are released.
        /// </summary>
        ~ClientX() {
            Dispose(disposing: false);
        }
    }
}
