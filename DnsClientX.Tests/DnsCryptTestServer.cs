using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX.DnsCrypt;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace DnsClientX.Tests;

/// <summary>Controlled socket peer; independent producer interoperability is qualified separately.</summary>
internal sealed class DnsCryptTestServer : IDisposable {
    private readonly UdpClient _udp;
    private readonly TcpListener _tcp;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _udpLoop;
    private readonly Task _tcpLoop;
    private readonly ConcurrentBag<Task> _clients = new();
    private readonly ConcurrentBag<TcpClient> _sockets = new();
    private readonly byte[] _resolverSecret = Enumerable.Range(0x20, 32).Select(value => (byte)value).ToArray();
    internal readonly ConcurrentBag<string> QueryIdentities = new();
    internal readonly TaskCompletionSource<IPEndPoint> UdpApplication = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource<bool> TcpApplication = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource<bool> TcpClosed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int CertificateQueries;
    internal int ApplicationQueries;
    internal bool HoldApplications;
    internal bool TruncateUdp;
    internal bool TruncateEvenUdp;
    internal bool SendInvalidUdpFirst;
    internal bool CorruptCertificates;
    internal byte[] Certificate;
    internal ushort System { get; }
    internal IPEndPoint Endpoint { get; }

    internal DnsCryptTestServer(ushort system = 2) {
        System = system;
        Certificate = SignedCertificate(system);
        _tcp = new TcpListener(IPAddress.Loopback, 0); _tcp.Start();
        Endpoint = (IPEndPoint)_tcp.LocalEndpoint;
        _udp = new UdpClient(Endpoint);
        _udpLoop = ServeUdp(); _tcpLoop = ServeTcp();
    }

    internal static byte[] SignedCertificate(ushort system, uint serial = 1, byte[]? resolverKey = null, int validSeconds = 3600) {
        var result = (byte[])DnsCryptVectorsTests.Certificate.Clone(); result[5] = (byte)system;
        if (resolverKey != null) Buffer.BlockCopy(resolverKey, 0, result, 72, 32);
        WriteUInt32(result, 112, serial);
        WriteUInt32(result, 116, (uint)DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeSeconds());
        WriteUInt32(result, 120, (uint)DateTimeOffset.UtcNow.AddSeconds(validSeconds).ToUnixTimeSeconds());
        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), 0));
        signer.BlockUpdate(result, 72, result.Length - 72); byte[] signature = signer.GenerateSignature();
        Buffer.BlockCopy(signature, 0, result, 8, 64); return result;
    }

    private async Task ServeUdp() {
        try {
            while (!_stop.IsCancellationRequested) {
                UdpReceiveResult incoming = await _udp.ReceiveAsync();
                byte[] inner = Unwrap(incoming.Buffer);
                bool application = !DnsWireMessageParser.TryParseHeader(inner, out var header) || !DnsWireMessageParser.TryParseQuestion(inner, 0, out var question) || question.Type != 16;
                if (application) {
                    UdpApplication.TrySetResult(incoming.RemoteEndPoint);
                    if (HoldApplications) continue;
                }
                byte[] reply = Reply(inner, application, false);
                if (application && SendInvalidUdpFirst) {
                    byte[] damaged = (byte[])reply.Clone(); damaged[8] ^= 1;
                    await _udp.SendAsync(damaged, damaged.Length, incoming.RemoteEndPoint);
                    damaged = (byte[])reply.Clone(); damaged[32] ^= 1;
                    await _udp.SendAsync(damaged, damaged.Length, incoming.RemoteEndPoint);
                }
                await _udp.SendAsync(reply, reply.Length, incoming.RemoteEndPoint);
            }
        } catch (Exception exception) when (_stop.IsCancellationRequested && (exception is SocketException || exception is ObjectDisposedException)) { }
    }

    private async Task ServeTcp() {
        try {
            while (!_stop.IsCancellationRequested) {
                TcpClient socket = await _tcp.AcceptTcpClientAsync(); _sockets.Add(socket);
                Task task = ServeClient(socket); _clients.Add(task);
            }
        } catch (Exception exception) when (_stop.IsCancellationRequested && (exception is SocketException || exception is ObjectDisposedException || exception is InvalidOperationException)) { }
    }

    private async Task ServeClient(TcpClient socket) {
        using (socket) {
            try {
                using NetworkStream stream = socket.GetStream();
                byte[] prefix = new byte[2]; await Exact(stream, prefix);
                byte[] packet = new byte[(prefix[0] << 8) | prefix[1]]; await Exact(stream, packet);
                byte[] inner = Unwrap(packet);
                bool application = !DnsWireMessageParser.TryParseQuestion(inner, 0, out var question) || question.Type != 16;
                if (application) {
                    TcpApplication.TrySetResult(true);
                    if (HoldApplications) { Assert.Equal(0, await stream.ReadAsync(new byte[1], 0, 1)); TcpClosed.TrySetResult(true); return; }
                }
                byte[] reply = Reply(inner, application, true);
                prefix[0] = (byte)(reply.Length >> 8); prefix[1] = (byte)reply.Length;
                // Deliberately split both the prefix and payload across writes.
                await stream.WriteAsync(prefix, 0, 1); await stream.WriteAsync(prefix, 1, 1);
                for (int offset = 0; offset < reply.Length; offset += 7) await stream.WriteAsync(reply, offset, Math.Min(7, reply.Length - offset));
            } catch (Exception exception) when (_stop.IsCancellationRequested && (exception is IOException || exception is ObjectDisposedException || exception is SocketException)) { }
        }
    }

    private byte[] Reply(byte[] inner, bool application, bool tcp) {
        if (!application) {
            Interlocked.Increment(ref CertificateQueries);
            byte[] cert = (byte[])Certificate.Clone(); if (CorruptCertificates) cert[8] ^= 1;
            var reader = new DnsWireReader(inner, 12); reader.ReadName(); reader.Skip(4);
            byte[] question = inner.Take(reader.Position).ToArray();
            question[2] = 0x81; question[3] = 0x80; question[7] = 1; question[11] = 0;
            // Each TXT record contains two binary strings rather than an encoded key.
            byte[] data = new byte[] { 62 }.Concat(cert.Take(62)).Concat(new byte[] { 62 }).Concat(cert.Skip(62)).ToArray();
            return question.Concat(new byte[] { 0xc0, 0x0c, 0, 16, 0, 1, 0, 0, 0, 60, 0, (byte)data.Length }).Concat(data).ToArray();
        }
        Interlocked.Increment(ref ApplicationQueries);
        QueryIdentities.Add(Convert.ToBase64String(inner.Skip(8).Take(44).ToArray()));
        byte[] shared = DnsCryptSodium.SharedKey(System, inner.Skip(8).Take(32).ToArray(), _resolverSecret);
        try {
            byte[] nonce = new byte[24]; Buffer.BlockCopy(inner, 40, nonce, 0, 12);
            byte[] dns = DnsCryptWire.Unpad(DnsCryptSodium.Decrypt(System, inner.Skip(52).ToArray(), nonce, shared));
            byte[] reply = (byte[])DnsCryptVectorsTests.Response.Clone(); reply[0] = dns[0]; reply[1] = dns[1];
            if (!tcp && (TruncateUdp || TruncateEvenUdp && (dns[1] & 1) == 0)) { reply = dns.Take(33).ToArray(); reply[2] = 0x83; reply[3] = 0x80; reply[7] = 0; reply[11] = 0; }
            byte[] serverNonce = DnsCryptSodium.Random(12); Buffer.BlockCopy(serverNonce, 0, nonce, 12, 12);
            byte[] encrypted = DnsCryptSodium.Encrypt(System, DnsCryptWire.Pad(reply, 64), nonce, shared);
            return DnsCryptVectorsTests.Hex("7236666e76576a38").Concat(nonce).Concat(encrypted).ToArray();
        } finally { DnsCryptSodium.Zero(shared); }
    }

    private static byte[] Unwrap(byte[] packet) => packet.Length >= 28 && packet.Take(8).All(value => value == 255) ? packet.Skip(28).ToArray() : packet;

    private static async Task Exact(Stream stream, byte[] bytes) {
        for (int offset = 0; offset < bytes.Length;) {
            int count = await stream.ReadAsync(bytes, offset, bytes.Length - offset);
            if (count == 0) throw new EndOfStreamException(); offset += count;
        }
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value) {
        bytes[offset] = (byte)(value >> 24); bytes[offset + 1] = (byte)(value >> 16); bytes[offset + 2] = (byte)(value >> 8); bytes[offset + 3] = (byte)value;
    }

    public void Dispose() {
        _stop.Cancel(); _udp.Dispose(); _tcp.Stop();
        foreach (TcpClient socket in _sockets) socket.Dispose();
        Task.WhenAll(_clients.Append(_udpLoop).Append(_tcpLoop)).GetAwaiter().GetResult();
        _stop.Dispose(); DnsCryptSodium.Zero(_resolverSecret);
    }
}
