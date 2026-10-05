using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects actual bootstrap and pooled-stream admission, beyond outer waiter completion.</summary>
[Collection("NoParallel")]
public class DnsCacheFlightTransportTests {
    /// <summary>The final canceled hostname query stops its explicit bootstrap UDP transport.</summary>
    [Fact]
    public async Task FinalCanceledBootstrapWaiterReleasesUdpSocket() {
        using var bootstrap = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(bootstrap, 53);
        using var caller = new CancellationTokenSource();
        var run = client.Resolve("answer.example", retryOnTransient: false, cancellationToken: caller.Token);
        var packet = await Bounded(bootstrap.ReceiveAsync());
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        using var replacement = new UdpClient(AddressFamily.InterNetwork);
        replacement.Client.ExclusiveAddressUse = true;
        replacement.Client.Bind(packet.RemoteEndPoint);
    }

    /// <summary>Canceling one owner preserves a shared bootstrap lookup for another client.</summary>
    [Fact]
    public async Task BootstrapSurvivorRemainsUsableAfterOtherOwnerCancels() {
        using var bootstrap = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var target = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var firstClient = CreateClient(bootstrap, ((IPEndPoint)target.Client.LocalEndPoint!).Port);
        using var secondClient = new ClientX(firstClient.EndpointConfiguration, enableCache: true);
        using var caller = new CancellationTokenSource();
        var first = firstClient.Resolve("first.example", retryOnTransient: false, cancellationToken: caller.Token);
        var bootstrapPacket = await Bounded(bootstrap.ReceiveAsync());
        var second = secondClient.Resolve("second.example", retryOnTransient: false);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.False(second.IsCompleted);
        var addresses = Answer(bootstrapPacket.Buffer, new byte[] { 127, 0, 0, 1 });
        await bootstrap.SendAsync(addresses, addresses.Length, bootstrapPacket.RemoteEndPoint);
        var targetPacket = await Bounded(target.ReceiveAsync());
        var response = Answer(targetPacket.Buffer, new byte[] { 192, 0, 2, 1 });
        await target.SendAsync(response, response.Length, targetPacket.RemoteEndPoint);
        Assert.Equal(DnsResponseCode.NoError, (await second).Status);
        Assert.Equal(0, bootstrap.Available);
    }

    /// <summary>A canceled pooled TCP loser retains endpoint admission until its late response is drained.</summary>
    [Fact]
    public async Task PooledTcpLoserRetainsAdmissionUntilTransactionCleanup() {
        var slow = new TcpListener(IPAddress.Loopback, 0);
        slow.Start();
        using var fast = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try {
            using var resolver = new DnsMultiResolver(new[] {
                new DnsResolverEndpoint { Host = "127.0.0.1", Port = ((IPEndPoint)slow.LocalEndpoint).Port, Transport = Transport.Tcp },
                new DnsResolverEndpoint { Host = "127.0.0.1", Port = ((IPEndPoint)fast.Client.LocalEndPoint!).Port }
            }, new MultiResolverOptions { EnableResponseCache = true, PerEndpointMaxInFlight = 1, MaxParallelism = 2 });
            var first = resolver.QueryAsync(Guid.NewGuid().ToString("N") + ".example", DnsRecordType.A, deadline.Token);
            using var peer = await Bounded(slow.AcceptTcpClientAsync());
            using var stream = peer.GetStream();
            var firstSlow = await ReadFrame(stream, deadline.Token);
            var firstFast = await Bounded(fast.ReceiveAsync());
            var firstAnswer = Answer(firstFast.Buffer, new byte[] { 192, 0, 2, 1 });
            await fast.SendAsync(firstAnswer, firstAnswer.Length, firstFast.RemoteEndPoint);
            Assert.Equal(DnsResponseCode.NoError, (await first).Status);

            var second = resolver.QueryAsync(Guid.NewGuid().ToString("N") + ".example", DnsRecordType.A, deadline.Token);
            var secondFast = await Bounded(fast.ReceiveAsync());
            var nextSlow = ReadFrame(stream, deadline.Token);
            var observation = Task.Delay(100, deadline.Token);
            Assert.Same(observation, await Task.WhenAny(nextSlow, observation));
            var late = Answer(firstSlow, new byte[] { 192, 0, 2, 1 });
            await WriteFrame(stream, late, deadline.Token);
            await Bounded(nextSlow);
            var secondAnswer = Answer(secondFast.Buffer, new byte[] { 192, 0, 2, 1 });
            await fast.SendAsync(secondAnswer, secondAnswer.Length, secondFast.RemoteEndPoint);
            Assert.Equal(DnsResponseCode.NoError, (await second).Status);
        } finally {
            deadline.Cancel();
            slow.Stop();
        }
    }

    private static ClientX CreateClient(UdpClient bootstrap, int port) {
        var client = new ClientX("resolver-" + Guid.NewGuid().ToString("N") + ".invalid", DnsRequestFormat.DnsOverUDP,
            timeOutMilliseconds: 2000, enableCache: true);
        client.EndpointConfiguration.Port = port;
        client.EndpointConfiguration.BootstrapResolver = new DnsResolverEndpoint {
            Host = "127.0.0.1", Port = ((IPEndPoint)bootstrap.Client.LocalEndPoint!).Port
        };
        return client;
    }

    private static async Task<T> Bounded<T>(Task<T> task) {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(3000)));
        return await task;
    }

    private static async Task<byte[]> ReadFrame(Stream stream, CancellationToken token) {
        async Task ReadExact(byte[] bytes) {
            var offset = 0;
            while (offset < bytes.Length) {
                var count = await stream.ReadAsync(bytes, offset, bytes.Length - offset, token);
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
        }
        var prefix = new byte[2];
        await ReadExact(prefix);
        var message = new byte[(prefix[0] << 8) | prefix[1]];
        await ReadExact(message);
        return message;
    }

    private static async Task WriteFrame(Stream stream, byte[] message, CancellationToken token) {
        var frame = new byte[message.Length + 2];
        frame[0] = (byte)(message.Length >> 8);
        frame[1] = (byte)message.Length;
        message.CopyTo(frame, 2);
        await stream.WriteAsync(frame, 0, frame.Length, token);
        await stream.FlushAsync(token);
    }

    private static byte[] Answer(byte[] query, byte[] address) {
        var end = 12;
        while (query[end] != 0) end += query[end] + 1;
        end += 5;
        var record = new byte[] { 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, address[0], address[1], address[2], address[3] };
        var response = new byte[end + record.Length];
        Array.Copy(query, response, end);
        response[2] = 0x81; response[3] = 0x80;
        response[6] = 0; response[7] = 1;
        Array.Clear(response, 8, 4);
        record.CopyTo(response, end);
        return response;
    }
}
