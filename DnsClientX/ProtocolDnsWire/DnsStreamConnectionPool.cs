using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Owns persistent RFC 7766 TCP and DoT connections for one <see cref="ClientX"/> instance.
    /// </summary>
    internal sealed class DnsStreamConnectionPool : IDisposable, IAsyncDisposable {
        private readonly ConcurrentDictionary<PoolKey, DnsStreamConnection> _connections = new();
        private int _disposed;

        internal Task<byte[]> QueryTcpAsync(IPAddress address, int port, IPEndPoint? localEndPoint,
            byte[] query, int timeoutMilliseconds, int maxInFlight, CancellationToken cancellationToken) {
            var key = new PoolKey(address, port, localEndPoint, false, string.Empty, false,
                SslProtocols.None, maxInFlight);
            return QueryAsync(key, query, timeoutMilliseconds, cancellationToken);
        }

        internal Task<byte[]> QueryTlsAsync(IPAddress address, int port, IPEndPoint? localEndPoint,
            string tlsServerName, bool ignoreCertificateErrors, SslProtocols protocols,
            byte[] query, int timeoutMilliseconds, int maxInFlight, CancellationToken cancellationToken) {
            var key = new PoolKey(address, port, localEndPoint, true, tlsServerName,
                ignoreCertificateErrors, protocols, maxInFlight);
            return QueryAsync(key, query, timeoutMilliseconds, cancellationToken);
        }

        private async Task<byte[]> QueryAsync(PoolKey key, byte[] query, int timeoutMilliseconds,
            CancellationToken cancellationToken) {
            ThrowIfDisposed();
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (query.Length < 2 || query.Length > ushort.MaxValue) {
                throw new ArgumentException("A stream DNS query must contain a transaction ID and fit in 65535 octets.", nameof(query));
            }
            if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));

            Exception? firstFailure = null;
            for (int attempt = 0; attempt < 2; attempt++) {
                ThrowIfDisposed();
                DnsStreamConnection connection = _connections.GetOrAdd(key,
                    item => new DnsStreamConnection(item));
                if (Volatile.Read(ref _disposed) != 0) {
                    if (_connections.TryRemove(key, out DnsStreamConnection? removed)) removed.Dispose();
                    ThrowIfDisposed();
                }
                try {
                    return await connection.QueryAsync(query, timeoutMilliseconds, cancellationToken)
                        .ConfigureAwait(false);
                } catch (DnsStreamConnectionException exception) when (attempt == 0 && !cancellationToken.IsCancellationRequested) {
                    firstFailure = exception;
                    if (_connections.TryRemove(key, out DnsStreamConnection? removed)) {
                        removed.Dispose();
                    }
                }
            }
            throw firstFailure ?? new DnsStreamConnectionException("The DNS stream query failed after reconnecting.");
        }

        private void ThrowIfDisposed() {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(DnsStreamConnectionPool));
        }

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (DnsStreamConnection connection in _connections.Values) {
                connection.Dispose();
            }
            _connections.Clear();
        }

        public ValueTask DisposeAsync() {
            Dispose();
            return default;
        }

        internal readonly struct PoolKey : IEquatable<PoolKey> {
            internal PoolKey(IPAddress address, int port, IPEndPoint? localEndPoint, bool useTls,
                string tlsServerName, bool ignoreCertificateErrors, SslProtocols protocols, int maxInFlight) {
                Address = address ?? throw new ArgumentNullException(nameof(address));
                Port = port;
                LocalAddress = localEndPoint?.Address;
                LocalPort = localEndPoint?.Port ?? 0;
                UseTls = useTls;
                TlsServerName = tlsServerName ?? string.Empty;
                IgnoreCertificateErrors = ignoreCertificateErrors;
                Protocols = protocols;
                MaxInFlight = maxInFlight > 0 ? maxInFlight : throw new ArgumentOutOfRangeException(nameof(maxInFlight));
            }

            internal IPAddress Address { get; }
            internal int Port { get; }
            internal IPAddress? LocalAddress { get; }
            internal int LocalPort { get; }
            internal bool UseTls { get; }
            internal string TlsServerName { get; }
            internal bool IgnoreCertificateErrors { get; }
            internal SslProtocols Protocols { get; }
            internal int MaxInFlight { get; }
            internal IPEndPoint? LocalEndPoint => LocalAddress == null ? null : new IPEndPoint(LocalAddress, LocalPort);

            public bool Equals(PoolKey other) =>
                Address.Equals(other.Address) && Port == other.Port &&
                Equals(LocalAddress, other.LocalAddress) && LocalPort == other.LocalPort &&
                UseTls == other.UseTls &&
                string.Equals(TlsServerName, other.TlsServerName, StringComparison.OrdinalIgnoreCase) &&
                IgnoreCertificateErrors == other.IgnoreCertificateErrors && Protocols == other.Protocols &&
                MaxInFlight == other.MaxInFlight;

            public override bool Equals(object? obj) => obj is PoolKey other && Equals(other);

            public override int GetHashCode() {
                unchecked {
                    int hash = Address.GetHashCode();
                    hash = (hash * 397) ^ Port;
                    hash = (hash * 397) ^ (LocalAddress?.GetHashCode() ?? 0);
                    hash = (hash * 397) ^ LocalPort;
                    hash = (hash * 397) ^ UseTls.GetHashCode();
                    hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(TlsServerName);
                    hash = (hash * 397) ^ IgnoreCertificateErrors.GetHashCode();
                    hash = (hash * 397) ^ (int)Protocols;
                    hash = (hash * 397) ^ MaxInFlight;
                    return hash;
                }
            }
        }

        private sealed class DnsStreamConnection : IDisposable {
            private readonly PoolKey _key;
            private readonly ConcurrentDictionary<ushort, PendingQuery> _pending = new();
            private readonly SemaphoreSlim _connectGate = new(1, 1);
            private readonly SemaphoreSlim _writeGate = new(1, 1);
            private readonly SemaphoreSlim _capacity;
            private TcpClient? _client;
            private Stream? _stream;
            private CancellationTokenSource? _lifetime;
            private Task? _readerTask;
            private int _faulted;
            private int _disposed;

            internal DnsStreamConnection(PoolKey key) {
                _key = key;
                _capacity = new SemaphoreSlim(key.MaxInFlight, key.MaxInFlight);
            }

            internal async Task<byte[]> QueryAsync(byte[] query, int timeoutMilliseconds,
                CancellationToken cancellationToken) {
                ThrowIfDisposed();
                await _capacity.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    await EnsureConnectedAsync(timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                    ushort transactionId = (ushort)((query[0] << 8) | query[1]);
                    PendingQuery pending = await ReserveTransactionIdAsync(transactionId, cancellationToken)
                        .ConfigureAwait(false);

                    try {
                        await WriteQueryAsync(query, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                    } catch (Exception exception) {
                        _pending.TryRemove(transactionId, out _);
                        var failure = ToConnectionException(exception, "Writing the framed DNS query failed.");
                        pending.Completion.TrySetException(failure);
                        Fault(failure);
                        throw failure;
                    }

                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    deadline.CancelAfter(timeoutMilliseconds);
                    Task timeoutTask = Task.Delay(Timeout.Infinite, deadline.Token);
                    Task completed = await Task.WhenAny(pending.Completion.Task, timeoutTask).ConfigureAwait(false);
                    if (completed == pending.Completion.Task || pending.Completion.Task.IsCompleted) {
                        return await pending.Completion.Task.ConfigureAwait(false);
                    }

                    if (_pending.TryRemove(transactionId, out PendingQuery? removed)) {
                        removed.Completion.TrySetCanceled();
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"The DNS stream query timed out after {timeoutMilliseconds} milliseconds.");
                } finally {
                    _capacity.Release();
                }
            }

            private async Task<PendingQuery> ReserveTransactionIdAsync(ushort transactionId,
                CancellationToken cancellationToken) {
                while (true) {
                    var pending = new PendingQuery();
                    if (_pending.TryAdd(transactionId, pending)) return pending;
                    if (_pending.TryGetValue(transactionId, out PendingQuery? existing)) {
                        try {
                            await WaitWithCancellationAsync(existing.Completion.Task, cancellationToken)
                                .ConfigureAwait(false);
                        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                            throw;
                        } catch {
                            // The transaction ID becomes reusable after the earlier request completes or fails.
                        }
                    }
                }
            }

            private async Task EnsureConnectedAsync(int timeoutMilliseconds, CancellationToken cancellationToken) {
                if (_stream != null && Volatile.Read(ref _faulted) == 0) return;
                await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    ThrowIfDisposed();
                    if (_stream != null && Volatile.Read(ref _faulted) == 0) return;
                    CloseTransport();

                    TcpClient client = DnsWireResolveTcp.TcpClientFactory(_key.Address.AddressFamily);
                    Stream? stream = null;
                    try {
                        SocketBinding.Bind(client.Client, _key.LocalEndPoint, _key.Address.AddressFamily);
                        await ConnectAsync(client, _key.Address, _key.Port, timeoutMilliseconds, cancellationToken)
                            .ConfigureAwait(false);
                        stream = client.GetStream();
                        if (_key.UseTls) {
                            var sslStream = new SslStream(stream, false,
                                (_, _, _, errors) => errors == SslPolicyErrors.None || _key.IgnoreCertificateErrors);
                            await AuthenticateAsync(sslStream, _key.TlsServerName, _key.Protocols,
                                timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                            stream = sslStream;
                        }

                        var lifetime = new CancellationTokenSource();
                        _client = client;
                        _stream = stream;
                        _lifetime = lifetime;
                        Volatile.Write(ref _faulted, 0);
                        _readerTask = Task.Run(() => ReadLoopAsync(stream, lifetime.Token));
                    } catch (Exception exception) {
                        stream?.Dispose();
                        client.Dispose();
                        throw ToConnectionException(exception, $"Connecting to {_key.Address}:{_key.Port} failed.");
                    }
                } finally {
                    _connectGate.Release();
                }
            }

            private async Task WriteQueryAsync(byte[] query, int timeoutMilliseconds,
                CancellationToken cancellationToken) {
                await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    Stream stream = _stream ?? throw new DnsStreamConnectionException("The DNS stream is not connected.");
                    var frame = new byte[query.Length + 2];
                    frame[0] = (byte)(query.Length >> 8);
                    frame[1] = (byte)query.Length;
                    Buffer.BlockCopy(query, 0, frame, 2, query.Length);
                    await WaitWithTimeoutAsync(
                        stream.WriteAsync(frame, 0, frame.Length, cancellationToken),
                        timeoutMilliseconds,
                        cancellationToken,
                        "Writing to the DNS stream").ConfigureAwait(false);
                    await WaitWithTimeoutAsync(stream.FlushAsync(cancellationToken), timeoutMilliseconds,
                        cancellationToken, "Flushing the DNS stream").ConfigureAwait(false);
                } finally {
                    _writeGate.Release();
                }
            }

            private async Task ReadLoopAsync(Stream stream, CancellationToken cancellationToken) {
                try {
                    var length = new byte[2];
                    while (!cancellationToken.IsCancellationRequested) {
                        await DnsWire.ReadExactAsync(stream, length, 0, 2, cancellationToken).ConfigureAwait(false);
                        int messageLength = (length[0] << 8) | length[1];
                        if (messageLength < 2) {
                            throw new DnsStreamConnectionException("A DNS stream response is shorter than its transaction ID.");
                        }
                        var response = new byte[messageLength];
                        await DnsWire.ReadExactAsync(stream, response, 0, response.Length, cancellationToken)
                            .ConfigureAwait(false);
                        ushort transactionId = (ushort)((response[0] << 8) | response[1]);
                        if (_pending.TryRemove(transactionId, out PendingQuery? pending)) {
                            pending.Completion.TrySetResult(response);
                        }
                        // An unmatched response can be a late response for a canceled/timed-out query.
                        // It is consumed but cannot complete another transaction.
                    }
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    // Expected when the owning client or this connection is disposed.
                } catch (Exception exception) {
                    Fault(ToConnectionException(exception, "Reading a framed DNS response failed."));
                }
            }

            private void Fault(DnsStreamConnectionException exception) {
                if (Interlocked.Exchange(ref _faulted, 1) != 0) return;
                CloseTransport();
                foreach (var item in _pending) {
                    if (_pending.TryRemove(item.Key, out PendingQuery? pending)) {
                        pending.Completion.TrySetException(exception);
                    }
                }
            }

            private void CloseTransport() {
                CancellationTokenSource? lifetime = Interlocked.Exchange(ref _lifetime, null);
                Stream? stream = Interlocked.Exchange(ref _stream, null);
                TcpClient? client = Interlocked.Exchange(ref _client, null);
                _readerTask = null;
                try { lifetime?.Cancel(); } catch (ObjectDisposedException) { }
                try { stream?.Dispose(); } catch { }
                try { client?.Dispose(); } catch { }
                lifetime?.Dispose();
            }

            private void ThrowIfDisposed() {
                if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(DnsStreamConnection));
            }

            public void Dispose() {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                CloseTransport();
                var exception = new ObjectDisposedException(nameof(DnsStreamConnection));
                foreach (var item in _pending) {
                    if (_pending.TryRemove(item.Key, out PendingQuery? pending)) {
                        pending.Completion.TrySetException(exception);
                    }
                }
                // Queries may still be unwinding and releasing these semaphores. Their managed
                // wait handles are never allocated unless requested, so leave final reclamation to GC.
            }

            private static async Task ConnectAsync(TcpClient client, IPAddress address, int port,
                int timeoutMilliseconds, CancellationToken cancellationToken) {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(timeoutMilliseconds);
#if NET5_0_OR_GREATER
                try {
                    await client.ConnectAsync(address, port, deadline.Token).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"Connection to {address}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                }
#else
                Task connectTask = client.ConnectAsync(address, port);
                Task completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, deadline.Token))
                    .ConfigureAwait(false);
                if (completed != connectTask) {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"Connection to {address}:{port} timed out after {timeoutMilliseconds} milliseconds.");
                }
                await connectTask.ConfigureAwait(false);
#endif
            }

            private static async Task AuthenticateAsync(SslStream stream, string host, SslProtocols protocols,
                int timeoutMilliseconds, CancellationToken cancellationToken) {
                await WaitWithTimeoutAsync(
                    stream.AuthenticateAsClientAsync(host, null, protocols, false),
                    timeoutMilliseconds,
                    cancellationToken,
                    $"TLS authentication with {host}").ConfigureAwait(false);
            }

            private static async Task WaitWithTimeoutAsync(Task task, int timeoutMilliseconds,
                CancellationToken cancellationToken, string operationName) {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(timeoutMilliseconds);
                Task completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, deadline.Token))
                    .ConfigureAwait(false);
                if (completed != task) {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException($"{operationName} timed out after {timeoutMilliseconds} milliseconds.");
                }
                await task.ConfigureAwait(false);
            }

            private static async Task WaitWithCancellationAsync(Task task, CancellationToken cancellationToken) {
                if (task.IsCompleted) {
                    await task.ConfigureAwait(false);
                    return;
                }
                Task completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken))
                    .ConfigureAwait(false);
                if (completed != task) throw new OperationCanceledException(cancellationToken);
                await task.ConfigureAwait(false);
            }

            private static DnsStreamConnectionException ToConnectionException(Exception exception, string message) =>
                exception as DnsStreamConnectionException ?? new DnsStreamConnectionException(message, exception);

            private sealed class PendingQuery {
                internal TaskCompletionSource<byte[]> Completion { get; } =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    internal sealed class DnsStreamConnectionException : IOException {
        internal DnsStreamConnectionException(string message) : base(message) { }
        internal DnsStreamConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }
}
