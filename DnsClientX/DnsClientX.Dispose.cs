using System;
using System.Collections.Generic;
using System.Net.Http;

namespace DnsClientX {
    public partial class ClientX : IDisposable {
        private bool _disposed;
        private readonly HashSet<HttpClient> _disposedClients = new();

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
                    lock (_lock) {
                        foreach (HttpClient client in _clients.Values) {
                            if (_disposedClients.Add(client)) {
                                client.Dispose();
                            }
                        }
                        _clients.Clear();

                        if (Client != null && _disposedClients.Add(Client)) {
                            Client.Dispose();
                        }

                        handler?.Dispose();
                    }
                }

                _disposed = true;
            }
        }

        ~ClientX() {
            Dispose(disposing: false);
        }
    }
}
