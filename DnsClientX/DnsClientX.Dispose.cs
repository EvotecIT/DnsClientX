using System;
using System.Net.Http;

namespace DnsClientX {
    public partial class ClientX : IDisposable {
        private bool _disposed;

        /// <summary>
        /// Releases the unmanaged resources used by the client and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;

            if (disposing) {
                lock (_lock) {
                    foreach (var client in _clients.Values) {
                        client.Dispose();
                    }
                    _clients.Clear();

                    Client?.Dispose();
                    Client = null;
                    handler?.Dispose();
                    handler = null;
                }
            }

            _disposed = true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
