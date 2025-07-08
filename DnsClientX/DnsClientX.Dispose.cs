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
        private readonly HashSet<HttpClient> _disposedClients = new();
        internal static int DisposalCount;

        private bool TryAddDisposedClient(HttpClient client) {
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
                    }

                    handlerLocal?.Dispose();
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

                foreach (HttpClient client in clients) {
                    if (TryAddDisposedClient(client)) {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        if (client is IAsyncDisposable asyncClient) {
                            await asyncClient.DisposeAsync().ConfigureAwait(false);
                        } else {
                            client.Dispose();
                        }
#else
                        client.Dispose();
#endif
                    }
                }

                if (mainClient != null && TryAddDisposedClient(mainClient)) {
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                    if (mainClient is IAsyncDisposable asyncClient) {
                        await asyncClient.DisposeAsync().ConfigureAwait(false);
                    } else {
                        mainClient.Dispose();
                    }
#else
                    mainClient.Dispose();
#endif
                }

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                if (handlerLocal != null) {
                    if (handlerLocal is IAsyncDisposable asyncHandler) {
                        await asyncHandler.DisposeAsync().ConfigureAwait(false);
                    } else {
                        handlerLocal.Dispose();
                    }
                }
#else
                if (handlerLocal != null) {
                    handlerLocal.Dispose();
                }
#endif

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
