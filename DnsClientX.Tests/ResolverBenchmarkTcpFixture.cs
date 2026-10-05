using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests;

// Real correlated DNS responses let tests observe connection reuse without answer-cache hits.
internal sealed class ResolverBenchmarkTcpFixture {
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new(TimeSpan.FromSeconds(10));
    private readonly ConcurrentBag<TcpClient> _clients = new();
    private readonly List<Task> _handlers = new();
    private readonly SemaphoreSlim _closedSignal = new(0);
    private readonly Task _accept;
    private int _queries, _connections, _closed;
    internal int Port { get; }
    internal int Queries => Volatile.Read(ref _queries);
    internal int Connections => Volatile.Read(ref _connections);
    internal ResolverBenchmarkTcpFixture() {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accept = AcceptAsync();
    }
    private async Task AcceptAsync() {
        try {
            while (!_stop.IsCancellationRequested) {
                TcpClient client = await _listener.AcceptTcpClientAsync();
                _clients.Add(client);
                Interlocked.Increment(ref _connections);
                _handlers.Add(ServeAsync(client));
            }
        } catch (Exception) when (_stop.IsCancellationRequested) { }
    }
    private async Task ServeAsync(TcpClient client) {
        using (client) {
            try {
                NetworkStream stream = client.GetStream();
                while (true) {
                    byte[] prefix = new byte[2];
                    if (!await ReadAsync(stream, prefix)) { return; }
                    byte[] query = new byte[(prefix[0] << 8) | prefix[1]];
                    if (!await ReadAsync(stream, query)) { return; }
                    int end = 12;
                    while (query[end] != 0) { end += query[end] + 1; }
                    end += 5;
                    byte[] response = new byte[end];
                    Array.Copy(query, response, end);
                    response[2] = 0x81; response[3] = 0x80;
                    Array.Clear(response, 6, 6);
                    Interlocked.Increment(ref _queries);
                    prefix[0] = (byte)(end >> 8); prefix[1] = (byte)end;
                    await stream.WriteAsync(prefix, 0, 2, _stop.Token);
                    await stream.WriteAsync(response, 0, end, _stop.Token);
                }
            } catch (IOException) { // A canceled client may reset its owned connection.
            } catch (Exception) when (_stop.IsCancellationRequested) { }
            finally { Interlocked.Increment(ref _closed); _closedSignal.Release(); }
        }
    }
    private async Task<bool> ReadAsync(NetworkStream stream, byte[] bytes) {
        int offset = 0;
        while (offset < bytes.Length) {
            int count = await stream.ReadAsync(bytes, offset, bytes.Length - offset, _stop.Token);
            if (count == 0) { return false; }
            offset += count;
        }
        return true;
    }
    internal async Task WaitForClosureAsync(int count) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Volatile.Read(ref _closed) < count) { await _closedSignal.WaitAsync(timeout.Token); }
    }
    internal async Task DisposeAsync() {
        _stop.Cancel(); _listener.Stop();
        await _accept;
        foreach (TcpClient client in _clients) { client.Dispose(); }
        try { await Task.WhenAll(_handlers); }
        finally { _stop.Dispose(); _closedSignal.Dispose(); }
    }
}
