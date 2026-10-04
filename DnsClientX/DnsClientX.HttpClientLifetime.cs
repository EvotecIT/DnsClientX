using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace DnsClientX;

public partial class ClientX {
    private const int MaxRetainedHttpClients = 8;
    private readonly Dictionary<HttpClient, int> _httpClientUseCounts = new();

    private HttpClientLease AcquireHttpClient(Configuration configuration, bool certificatePolicy) {
        lock (_lock) {
            HttpClient client = GetClient(configuration, certificatePolicy);
            _httpClientUseCounts.TryGetValue(client, out int count);
            _httpClientUseCounts[client] = count + 1;
            return new HttpClientLease(this, client);
        }
    }

    private void ReleaseHttpClient(HttpClient client) {
        lock (_lock) {
            if (!_httpClientUseCounts.TryGetValue(client, out int count)) return;
            if (count == 1) _httpClientUseCounts.Remove(client);
            else _httpClientUseCounts[client] = count - 1;
            TrimManagedHttpClients(Client);
        }
    }

    // Call only under the ownership lock. Concurrent requests may temporarily exceed the idle
    // pool bound; releasing their leases reclaims superseded clients without aborting requests.
    private void TrimManagedHttpClients(HttpClient? selected) {
        if (_managedClients.Count <= MaxRetainedHttpClients) return;
        foreach (HttpClient candidate in _managedClients.ToArray()) {
            if (ReferenceEquals(candidate, selected) || _httpClientUseCounts.ContainsKey(candidate)) continue;
            _managedClients.Remove(candidate);
            _clientTlsPolicies.Remove(candidate);
            foreach (DnsSelectionStrategy strategy in _clients.Where(item => ReferenceEquals(item.Value, candidate)).Select(item => item.Key).ToArray()) {
                _clients.Remove(strategy);
            }
            candidate.Dispose();
            if (_managedClients.Count <= MaxRetainedHttpClients) break;
        }
    }

    private sealed class HttpClientLease : IDisposable {
        private ClientX? _owner;
        internal HttpClientLease(ClientX owner, HttpClient client) { _owner = owner; Client = client; }
        internal HttpClient Client { get; }
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.ReleaseHttpClient(Client);
    }
}
