using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.DnsCrypt;

public sealed partial class DnsCryptClient {
    private async Task<byte[]> ExchangeAsync(byte[] inner, bool tcp, Func<byte[], bool> accept, CancellationToken token) {
        IPEndPoint endpoint = _options.RelayEndpoint ?? _options.ResolverEndpoint;
        byte[] packet = DnsCryptWire.WrapRelay(inner, _options.RelayEndpoint == null ? null : _options.ResolverEndpoint);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_options.Timeout);
        try {
            timeout.Token.ThrowIfCancellationRequested();
            return tcp ? await ExchangeTcpAsync(endpoint, packet, accept, timeout.Token).ConfigureAwait(false)
                : await ExchangeUdpAsync(endpoint, packet, accept, timeout.Token).ConfigureAwait(false);
        } catch (Exception exception) when (timeout.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("The DNSCrypt exchange timed out.", exception);
        }
    }

    private static async Task<byte[]> ExchangeUdpAsync(IPEndPoint endpoint, byte[] packet, Func<byte[], bool> accept, CancellationToken token) {
        using var socket = new UdpClient(endpoint.AddressFamily);
        using var registration = token.Register(socket.Dispose);
        socket.Connect(endpoint);
        token.ThrowIfCancellationRequested();
        await socket.SendAsync(packet, packet.Length).ConfigureAwait(false);
        while (true) {
            token.ThrowIfCancellationRequested();
            UdpReceiveResult result = await socket.ReceiveAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (result.Buffer.Length <= 4096 && accept(result.Buffer)) return result.Buffer;
        }
    }

    private static async Task<byte[]> ExchangeTcpAsync(IPEndPoint endpoint, byte[] packet, Func<byte[], bool> accept, CancellationToken token) {
        using var socket = new TcpClient(endpoint.AddressFamily);
        using var registration = token.Register(socket.Dispose);
        token.ThrowIfCancellationRequested();
        await socket.ConnectAsync(endpoint.Address, endpoint.Port).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        using NetworkStream stream = socket.GetStream();
        var prefix = new byte[] { (byte)(packet.Length >> 8), (byte)packet.Length };
        await stream.WriteAsync(prefix, 0, 2, token).ConfigureAwait(false);
        await stream.WriteAsync(packet, 0, packet.Length, token).ConfigureAwait(false);
        await ReadExactAsync(stream, prefix, token).ConfigureAwait(false);
        int length = (prefix[0] << 8) | prefix[1];
        if (length < 12 || length > 4096) throw new InvalidDataException("The DNSCrypt TCP response length is outside the protocol bounds.");
        var response = new byte[length];
        await ReadExactAsync(stream, response, token).ConfigureAwait(false);
        if (!accept(response)) throw new InvalidDataException("The DNSCrypt TCP response is invalid or does not belong to this exchange.");
        return response;
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token) {
        for (int offset = 0; offset < buffer.Length;) {
            int received = await stream.ReadAsync(buffer, offset, buffer.Length - offset, token).ConfigureAwait(false);
            if (received == 0) throw new EndOfStreamException("The DNSCrypt TCP peer closed a partially framed response.");
            offset += received;
        }
    }
}
