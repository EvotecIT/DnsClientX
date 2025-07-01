using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class TcpConnectionStore {
        private static readonly ConcurrentDictionary<string, TcpClient> _connections = new();

        internal static async Task<TcpClient> GetConnectionAsync(string host, int port, Func<TcpClient, Task> connectAsync) {
            string key = $"{host}:{port}";
            if (_connections.TryGetValue(key, out var existing)) {
                if (existing.Connected) {
                    return existing;
                }

                RemoveConnection(key, existing);
            }

            var client = new TcpClient();
            await connectAsync(client);
            _connections[key] = client;
            return client;
        }

        private static void RemoveConnection(string key, TcpClient client) {
            try {
                client.Dispose();
            } catch {
                // Ignore disposal errors
            }

            _connections.TryRemove(key, out _);
        }

        internal static void DisposeAll() {
            foreach (var kvp in _connections) {
                RemoveConnection(kvp.Key, kvp.Value);
            }
        }
    }
}
