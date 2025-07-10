using System;
using System.Collections.Generic;
using System.Net.Http;

using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Partial <see cref="ClientX"/> class implementing disposal logic.
    /// </summary>
    public partial class ClientX : IDisposable, IAsyncDisposable {
        private bool _disposed;
        private readonly HashSet<object> _disposedResources = new();
        internal static int DisposalCount;

        private bool TryAddDisposedResource(object client) {
            lock (_lock) {
                return _disposedResources.Add(client);
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
                        if (TryAddDisposedResource(client)) {
                            client.Dispose();
                        }
                    }

                    if (mainClient != null && TryAddDisposedResource(mainClient)) {
                        mainClient.Dispose();
                        if (_handlerOwnedByClient && handlerLocal != null) {
                            TryAddDisposedResource(handlerLocal);
                        }
                    }

                    if (!_handlerOwnedByClient && handlerLocal != null && TryAddDisposedResource(handlerLocal)) {
                        handlerLocal.Dispose();
                    }

                    lock (_lock) {
                        _disposedResources.Clear();
                    }
                }

                System.Threading.Interlocked.Increment(ref DisposalCount);
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
                    if (TryAddDisposedResource(client)) {
                        if (client is IAsyncDisposable asyncClient) {
                            await asyncClient.DisposeAsync().ConfigureAwait(false);
                        } else {
                            client.Dispose();
                        }
                    }
                }
#else
                foreach (HttpClient client in clients) {
                    if (TryAddDisposedResource(client)) {
                        client.Dispose();
                    }
                }
#endif

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (mainClient != null && TryAddDisposedResource(mainClient)) {
                    if (mainClient is IAsyncDisposable asyncClient) {
                        await asyncClient.DisposeAsync().ConfigureAwait(false);
                    } else {
                        mainClient.Dispose();
                    }
                    if (_handlerOwnedByClient && handlerLocal != null) {
                        TryAddDisposedResource(handlerLocal);
                    }
                }
#else
                if (mainClient != null && TryAddDisposedResource(mainClient)) {
                    mainClient.Dispose();
                    if (_handlerOwnedByClient && handlerLocal != null) {
                        TryAddDisposedResource(handlerLocal);
                    }
                }
#endif

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (!_handlerOwnedByClient && handlerLocal != null && TryAddDisposedResource(handlerLocal)) {
                    if (handlerLocal is IAsyncDisposable asyncHandler) {
                        await asyncHandler.DisposeAsync().ConfigureAwait(false);
                    } else {
                        handlerLocal.Dispose();
                    }
                }
#else
                if (!_handlerOwnedByClient && handlerLocal != null && TryAddDisposedResource(handlerLocal)) {
                    handlerLocal.Dispose();
                }
#endif

                lock (_lock) {
                    _disposedResources.Clear();
                }

                _disposed = true;
                System.Threading.Interlocked.Increment(ref DisposalCount);
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
