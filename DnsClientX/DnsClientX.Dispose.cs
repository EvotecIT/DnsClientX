using System;
using System.Collections.Generic;
using System.Net.Http;

namespace DnsClientX {
    public partial class ClientX : IDisposable {
        private bool _disposed;
        private readonly HashSet<HttpClient> _disposedClients = new();

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
                if (disposing) {
                    HttpClientHandler? handlerLocal;
                    List<HttpClient> clients;
                    HttpClient? mainClient;

                    lock (_lock) {
                        clients = new List<HttpClient>(_clients.Values);
                        _clients.Clear();

                        mainClient = Client;
                        handlerLocal = handler;
                        Client = null;
                        handler = null;
                    }

                    foreach (HttpClient client in clients) {
                        if (TryAddDisposedClient(client)) {
                            client.Dispose();
                        }
                    }

                    if (mainClient != null && TryAddDisposedClient(mainClient)) {
                        mainClient.Dispose();
                    }

                    handlerLocal?.Dispose();
                    TcpConnectionStore.DisposeAll();
                }

                _disposed = true;
            }
        }

        ~ClientX() {
            Dispose(disposing: false);
        }
    }
}
