using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX {
    /// <summary>
    /// Reuses connected UDP sockets for sequential queries while leasing a distinct socket to
    /// each concurrent query. Failed and timed-out leases are discarded so late datagrams cannot
    /// be mistaken for a later query.
    /// </summary>
    internal sealed class DnsUdpClientPool : IDisposable {
        private const int MaxIdlePerEndpoint = 8;
        private readonly ConcurrentDictionary<PoolKey, ConcurrentBag<UdpClient>> clients = new();
        private readonly object lifecycleLock = new();
        private bool disposed;

        internal UdpClient Rent(IPAddress address, int port, IPEndPoint? localEndPoint, out PoolKey key) {
            ConcurrentBag<UdpClient> endpointClients;
            lock (lifecycleLock) {
                if (disposed) throw new ObjectDisposedException(nameof(DnsUdpClientPool));
                key = new PoolKey(address, port, localEndPoint);
                endpointClients = clients.GetOrAdd(key, _ => new ConcurrentBag<UdpClient>());
            }
            while (endpointClients.TryTake(out UdpClient? existing)) {
                try {
                    if (existing.Client.Connected) return existing;
                } catch (ObjectDisposedException) {
                    // A concurrent client disposal won the race; create a replacement below.
                }
                existing.Dispose();
            }

            var created = new UdpClient(address.AddressFamily);
            try {
                SocketBinding.Bind(created.Client, localEndPoint, address.AddressFamily);
                created.Connect(new IPEndPoint(address, port));
                return created;
            } catch {
                created.Dispose();
                throw;
            }
        }

        internal void Return(PoolKey key, UdpClient client, bool reusable) {
            if (!reusable) {
                client.Dispose();
                return;
            }

            lock (lifecycleLock) {
                if (disposed) {
                    client.Dispose();
                    return;
                }
                ConcurrentBag<UdpClient> endpointClients = clients.GetOrAdd(key, _ => new ConcurrentBag<UdpClient>());
                if (endpointClients.Count >= MaxIdlePerEndpoint) {
                    client.Dispose();
                    return;
                }
                endpointClients.Add(client);
            }
        }

        public void Dispose() {
            lock (lifecycleLock) {
                if (disposed) return;
                disposed = true;
                foreach (ConcurrentBag<UdpClient> endpointClients in clients.Values) {
                    while (endpointClients.TryTake(out UdpClient? client)) client.Dispose();
                }
                clients.Clear();
            }
        }

        internal readonly struct PoolKey : IEquatable<PoolKey> {
            private readonly IPAddress address;
            private readonly int port;
            private readonly IPAddress? localAddress;
            private readonly int localPort;

            internal PoolKey(IPAddress address, int port, IPEndPoint? localEndPoint) {
                this.address = address;
                this.port = port;
                localAddress = localEndPoint?.Address;
                localPort = localEndPoint?.Port ?? -1;
            }

            public bool Equals(PoolKey other) =>
                address.Equals(other.address)
                && port == other.port
                && Equals(localAddress, other.localAddress)
                && localPort == other.localPort;

            public override bool Equals(object? obj) => obj is PoolKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    int hash = address.GetHashCode();
                    hash = (hash * 397) ^ port;
                    hash = (hash * 397) ^ (localAddress?.GetHashCode() ?? 0);
                    return (hash * 397) ^ localPort;
                }
            }
        }
    }
}
