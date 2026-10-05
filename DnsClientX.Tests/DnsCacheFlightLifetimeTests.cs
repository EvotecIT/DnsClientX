using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects real transport ownership and captured deadlines across cached callers.</summary>
[Collection("NoParallel")]
public class DnsCacheFlightLifetimeTests {
    /// <summary>The final departing waiter releases its UDP socket before completion.</summary>
    [Fact]
    public async Task LastCanceledWaiterReturnsOnlyAfterRealUdpSocketIsReleased() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server, 2000);
        using var caller = new CancellationTokenSource();
        var run = client.Resolve(Guid.NewGuid().ToString("N") + ".example", retryOnTransient: false, cancellationToken: caller.Token);
        var packet = await ReceiveAsync(server);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

        // A completed canceled call must not retain an unanswered transport lease.
        using var replacement = new UdpClient(AddressFamily.InterNetwork);
        replacement.Client.ExclusiveAddressUse = true;
        replacement.Client.Bind(packet.RemoteEndPoint);
    }

    /// <summary>Different transport deadlines do not inherit an existing flight's timeout.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentCapturedTimeoutsDoNotShareExecutionDeadline(bool sameClient) {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var longClient = CreateClient(server, 800);
        using var shortClient = CreateClient(server, 50);
        var name = Guid.NewGuid().ToString("N") + ".example";
        var owner = longClient.Resolve(name, retryOnTransient: false);
        await ReceiveAsync(server);
        var elapsed = Stopwatch.StartNew();
        if (sameClient) {
            longClient.EndpointConfiguration.TimeOut = 50;
        }
        var response = await (sameClient ? longClient : shortClient).Resolve(name, retryOnTransient: false);
        Assert.InRange(elapsed.ElapsedMilliseconds, 0, 500);
        Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
        Assert.False(owner.IsCompleted);
        await ReceiveAsync(server);
        await owner;
    }

    /// <summary>One canceled waiter leaves a shared live transport and cache result intact.</summary>
    [Fact]
    public async Task CancelingOneRealUdpWaiterPreservesSurvivorAndCache() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server, 2000);
        using var caller = new CancellationTokenSource();
        var name = Guid.NewGuid().ToString("N") + ".example";
        var canceled = client.Resolve(name, retryOnTransient: false, cancellationToken: caller.Token);
        var packet = await ReceiveAsync(server);
        var survivor = client.Resolve(name, retryOnTransient: false);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.False(survivor.IsCompleted);
        var bytes = Answer(packet.Buffer);
        await server.SendAsync(bytes, bytes.Length, packet.RemoteEndPoint);
        var response = await survivor;
        Assert.Equal(DnsResponseSource.CoalescedNetwork, response.ResponseSource);
        Assert.Equal("192.0.2.1", Assert.Single(response.Answers).DataRaw);
        var cached = await client.Resolve(name, retryOnTransient: false);
        Assert.Equal(DnsResponseSource.Cache, cached.ResponseSource);
        Assert.Equal(0, server.Available);
    }

    /// <summary>Disposing one transport owner cannot cancel another client's execution; completed answers remain shared.</summary>
    [Fact]
    public async Task IndependentClientOwnersRetainTheirTransportAndShareCompletedAnswers() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var firstClient = CreateClient(server, 2000);
        using var secondClient = CreateClient(server, 2000);
        using var caller = new CancellationTokenSource();
        var name = Guid.NewGuid().ToString("N") + ".example";
        var first = firstClient.Resolve(name, retryOnTransient: false, cancellationToken: caller.Token);
        await ReceiveAsync(server);
        var second = secondClient.Resolve(name, retryOnTransient: false);
        var secondPacket = await ReceiveAsync(server);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        firstClient.Dispose();
        Assert.False(second.IsCompleted);

        var bytes = Answer(secondPacket.Buffer);
        await server.SendAsync(bytes, bytes.Length, secondPacket.RemoteEndPoint);
        var response = await second;
        Assert.Equal("192.0.2.1", Assert.Single(response.Answers).DataRaw);
        using var thirdClient = CreateClient(server, 2000);
        var cached = await thirdClient.Resolve(name, retryOnTransient: false);
        Assert.Equal(DnsResponseSource.Cache, cached.ResponseSource);
        Assert.Equal(0, server.Available);
    }

    /// <summary>Cached racing queries stop losing wire requests before releasing per-endpoint admission.</summary>
    [Fact]
    public async Task CachedResolverRaceReleasesLosingUdpSocketBeforeNextAdmission() {
        using var slow = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var fast = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var resolver = new DnsMultiResolver(new[] {
            new DnsResolverEndpoint { Host = "127.0.0.1", Port = ((IPEndPoint)slow.Client.LocalEndPoint!).Port },
            new DnsResolverEndpoint { Host = "127.0.0.1", Port = ((IPEndPoint)fast.Client.LocalEndPoint!).Port }
        }, new MultiResolverOptions {
            EnableResponseCache = true, PerEndpointMaxInFlight = 1, MaxParallelism = 2,
            DefaultTimeout = TimeSpan.FromSeconds(2)
        });
        IPEndPoint? previousLoser = null;
        for (var index = 0; index < 6; index++) {
            var run = resolver.QueryAsync(Guid.NewGuid().ToString("N") + ".example", DnsRecordType.A);
            var slowPacket = await ReceiveAsync(slow);
            // Returning the winner need not await losers; admitting the next flight must.
            if (previousLoser != null) {
                using var replacement = new UdpClient(AddressFamily.InterNetwork);
                replacement.Client.ExclusiveAddressUse = true;
                replacement.Client.Bind(previousLoser);
            }
            var fastPacket = await ReceiveAsync(fast);
            var bytes = Answer(fastPacket.Buffer);
            await fast.SendAsync(bytes, bytes.Length, fastPacket.RemoteEndPoint);
            Assert.Equal(DnsResponseCode.NoError, (await run).Status);
            previousLoser = slowPacket.RemoteEndPoint;
        }
    }

    private static ClientX CreateClient(UdpClient server, int milliseconds) {
        var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: milliseconds, enableCache: true);
        client.EndpointConfiguration.Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        return client;
    }

    private static async Task<UdpReceiveResult> ReceiveAsync(UdpClient server) {
        var receive = server.ReceiveAsync();
        Assert.Same(receive, await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(3))));
        return await receive;
    }

    private static byte[] Answer(byte[] query) {
        var end = 12;
        while (query[end] != 0) end += query[end] + 1;
        end += 5;
        var answer = new byte[] { 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 1 };
        var result = new byte[end + answer.Length];
        Array.Copy(query, result, end);
        result[2] = 0x81; result[3] = 0x80;
        result[6] = 0; result[7] = 1;
        Array.Clear(result, 8, 4);
        answer.CopyTo(result, end);
        return result;
    }
}
