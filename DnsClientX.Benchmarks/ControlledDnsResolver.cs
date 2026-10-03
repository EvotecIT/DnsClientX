using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace DnsClientX.Benchmarks;

/// <summary>A benchmark-owned loopback responder for equivalent UDP and TCP payloads.</summary>
internal sealed class ControlledDnsResolver : IAsyncDisposable {
    private readonly UdpClient _udp = new(new IPEndPoint(IPAddress.Loopback, 0));
    private readonly TcpListener _tcp;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _peers = new();
    private readonly Task _udpLoop;
    private readonly Task _tcpLoop;
    private readonly int _answerCount;
    internal int Port => ((IPEndPoint)_udp.Client.LocalEndPoint!).Port;

    internal ControlledDnsResolver(int answerCount) {
        _answerCount = answerCount;
        _tcp = new TcpListener(IPAddress.Loopback, Port);
        _tcp.Start();
        _udpLoop = ReceiveUdpAsync();
        _tcpLoop = AcceptTcpAsync();
    }

    private async Task ReceiveUdpAsync() {
        try {
            while (!_stop.IsCancellationRequested) {
                var query = await _udp.ReceiveAsync(_stop.Token).ConfigureAwait(false);
                byte[] response = ControlledDnsMessages.CreateAResponse(query.Buffer, _answerCount);
                await _udp.SendAsync(response, query.RemoteEndPoint, _stop.Token).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }

    private async Task AcceptTcpAsync() {
        try {
            while (!_stop.IsCancellationRequested) {
                var peer = await _tcp.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                _peers.Add(RespondTcpAsync(peer));
            }
        } catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
          catch (SocketException) when (_stop.IsCancellationRequested) { }
    }

    private async Task RespondTcpAsync(TcpClient peer) {
        using (peer)
        using (NetworkStream stream = peer.GetStream()) {
            try {
                var length = new byte[2];
                while (!_stop.IsCancellationRequested) {
                    await stream.ReadExactlyAsync(length, _stop.Token).ConfigureAwait(false);
                    var query = new byte[BinaryPrimitives.ReadUInt16BigEndian(length)];
                    await stream.ReadExactlyAsync(query, _stop.Token).ConfigureAwait(false);
                    byte[] response = ControlledDnsMessages.CreateAResponse(query, _answerCount);
                    BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)response.Length));
                    await stream.WriteAsync(length, _stop.Token).ConfigureAwait(false);
                    await stream.WriteAsync(response, _stop.Token).ConfigureAwait(false);
                }
            } catch (EndOfStreamException) { }
              catch (IOException) { }
              catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        }
    }

    public async ValueTask DisposeAsync() {
        _stop.Cancel();
        _tcp.Stop();
        await Task.WhenAll(_udpLoop, _tcpLoop).ConfigureAwait(false);
        await Task.WhenAll(_peers).ConfigureAwait(false);
        _udp.Dispose(); _stop.Dispose();
    }
}
