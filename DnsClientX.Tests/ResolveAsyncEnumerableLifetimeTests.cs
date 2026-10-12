using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects bounded streaming, early disposal and shared transport cancellation through UDP.</summary>
[Collection("NoParallel")]
public class ResolveAsyncEnumerableLifetimeTests {
    /// <summary>The stream starts only its bounded window, yields the first completed query and drains on break.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletionOrderIsBoundedAndDisposalReleasesUdpTransport(bool enableCache) {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server, enableCache);
        string[] names = Enumerable.Range(0, 12).Select(index => index + "-" + Guid.NewGuid().ToString("N") + ".example").ToArray();
        var enumerator = client.ResolveAsyncEnumerable(names, DnsRecordType.A, retryOnTransient: false).GetAsyncEnumerator();
        try {
            var advancing = enumerator.MoveNextAsync().AsTask();
            var first = await Receive(server);
            var second = await Receive(server);
            Assert.False(advancing.IsCompleted);
            Assert.Equal(0, server.Available);
            byte[] answer = Answer(second.Buffer);
            await server.SendAsync(answer, answer.Length, second.RemoteEndPoint);
            Assert.True(await Bounded(advancing));
            Assert.Equal(QueryName(second.Buffer), Assert.Single(enumerator.Current.Questions).Name);
            await enumerator.DisposeAsync();
            using var replacement = new UdpClient(AddressFamily.InterNetwork);
            replacement.Client.ExclusiveAddressUse = true;
            replacement.Client.Bind(first.RemoteEndPoint);
            Assert.Equal(0, server.Available); // No admission after the yielded item or disposal.
        } finally {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>A stream owns its waiter, not another caller's shared query or eventual cache result.</summary>
    [Fact]
    public async Task DisposingStreamKeepsAnIndependentSingleFlightWaiterAlive() {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var client = CreateClient(server, enableCache: true);
        string prefix = Guid.NewGuid().ToString("N");
        string[] names = { "first-" + prefix + ".example", "shared-" + prefix + ".example", "never-" + prefix + ".example" };
        var enumerator = client.ResolveAsyncEnumerable(names, DnsRecordType.A, retryOnTransient: false).GetAsyncEnumerator();
        try {
            var advancing = enumerator.MoveNextAsync().AsTask();
            var first = await Receive(server);
            var shared = await Receive(server);
            if (QueryName(first.Buffer) != names[0]) (first, shared) = (shared, first);
            var survivor = client.Resolve(names[1], retryOnTransient: false);
            byte[] firstAnswer = Answer(first.Buffer);
            await server.SendAsync(firstAnswer, firstAnswer.Length, first.RemoteEndPoint);
            Assert.True(await Bounded(advancing));
            await enumerator.DisposeAsync();
            Assert.False(survivor.IsCompleted);
            byte[] sharedAnswer = Answer(shared.Buffer);
            await server.SendAsync(sharedAnswer, sharedAnswer.Length, shared.RemoteEndPoint);
            var result = await Bounded(survivor);
            Assert.Equal(DnsResponseSource.CoalescedNetwork, result.ResponseSource);
            Assert.Equal("192.0.2.1", Assert.Single(result.Answers).Data);
            Assert.Equal(DnsResponseSource.Cache, (await client.Resolve(names[1], retryOnTransient: false)).ResponseSource);
            Assert.Equal(0, server.Available);
        } finally {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>A failed query cancels and observes an admitted peer instead of leaving background work running.</summary>
    [Fact]
    public async Task QueryFailureCancelsAndObservesAdmittedPeers() {
        using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP);
        client.EndpointConfiguration.MaxConcurrency = 2;
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ResolverOverride = async (name, type, token) => {
            if (name == "fail.example") throw new InvalidOperationException("fixture failure");
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { canceled.TrySetResult(token.IsCancellationRequested); }
            return new DnsResponse();
        };
        var enumerator = client.ResolveAsyncEnumerable(new[] { "pending.example", "fail.example", "never.example" },
            DnsRecordType.A, retryOnTransient: false).GetAsyncEnumerator();
        try {
            await Assert.ThrowsAsync<InvalidOperationException>(() => enumerator.MoveNextAsync().AsTask());
            Assert.True(await Bounded(canceled.Task));
        } finally {
            await enumerator.DisposeAsync();
        }
    }

    /// <summary>Unconfigured completion-order streams keep a bounded default for large inputs.</summary>
    [Fact]
    public async Task UnconfiguredStreamDoesNotAdmitEveryName() {
        using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP);
        int entered = 0;
        int canceled = 0;
        client.ResolverOverride = async (name, type, token) => {
            Interlocked.Increment(ref entered);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Interlocked.Increment(ref canceled); }
            return new DnsResponse();
        };
        using var caller = new CancellationTokenSource();
        var enumerator = client.ResolveAsyncEnumerable(Enumerable.Range(0, 100).Select(index => index + ".example").ToArray(),
            DnsRecordType.A, retryOnTransient: false, cancellationToken: caller.Token).GetAsyncEnumerator();
        try {
            var advancing = enumerator.MoveNextAsync().AsTask();
            Assert.Equal(32, Volatile.Read(ref entered));
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => advancing);
            Assert.Equal(32, Volatile.Read(ref canceled));
        } finally {
            await enumerator.DisposeAsync();
        }
    }

    private static ClientX CreateClient(UdpClient server, bool enableCache) {
        var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, timeOutMilliseconds: 3000, enableCache: enableCache);
        client.EndpointConfiguration.Port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        client.EndpointConfiguration.MaxConcurrency = 2;
        return client;
    }
    private static Task<UdpReceiveResult> Receive(UdpClient server) => Bounded(server.ReceiveAsync());
    private static async Task<T> Bounded<T>(Task<T> task) {
        Assert.Same(task, await Task.WhenAny(task, Task.Delay(4000)));
        return await task;
    }
    private static byte[] Answer(byte[] query) {
        byte[] response = TestUtilities.CreateResponseFromQuery(query);
        response[7] = 1;
        return response.Concat(new byte[] { 0xc0, 0x0c, 0, 1, 0, 1, 0, 0, 0, 60, 0, 4, 192, 0, 2, 1 }).ToArray();
    }
    private static string QueryName(byte[] query) {
        var reader = new DnsWireReader(query, 12, query.Length);
        return DnsWireNameCodec.TrimTrailingRootDot(reader.ReadName());
    }
}
