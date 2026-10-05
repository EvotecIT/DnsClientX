#if NET8_0_OR_GREATER
#pragma warning disable CA2252, CA1416
using System.Net.Quic;
using System.Reflection;

namespace DnsClientX.Tests;

/// <summary>Checks pool admission and shutdown without requiring native QUIC support.</summary>
public class DnsQuicConnectionPoolTests {
    /// <summary>Caller cancellation remains cancellation when shutdown also closes admission.</summary>
    [Fact]
    public async Task CallerCancellationDuringShutdownIsNotReportedAsDisposal() {
        var pool = new DnsQuicConnectionPool();
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task pending = pool.GetAsync("endpoint", new QuicClientConnectionOptions(), async (_, token) => {
            entered.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Canceled attempt must not be published.");
        }, caller.Token).AsTask();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            caller.Cancel();
            Task closing = pool.DisposeAsync().AsTask();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            await closing.WaitAsync(TimeSpan.FromSeconds(5));
        } finally {
            caller.Cancel();
            try { await pending; } catch (Exception) { }
            await pool.DisposeAsync();
        }
    }

    /// <summary>Every public disposer waits for an admitted transport to finish unwinding.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicConcurrentDisposersWaitForTheTransportDrain(bool synchronousSecond) {
        var client = new ClientX();
        var pool = (DnsQuicConnectionPool)typeof(ClientX).GetField("_quicConnectionPool", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client)!;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task pending = pool.GetAsync("endpoint", new QuicClientConnectionOptions(), async (_, token) => {
            using var registration = token.Register(() => canceled.TrySetResult(true));
            entered.TrySetResult(true);
            await release.Task;
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Canceled attempt must not be published.");
        }, default).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task first = client.DisposeAsync().AsTask();
        Task second = Task.CompletedTask;
        try {
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            second = synchronousSecond ? Task.Run(() => { secondStarted.TrySetResult(true); client.Dispose(); }) : client.DisposeAsync().AsTask();
            if (synchronousSecond) await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task held = Task.Delay(100);
            Assert.Same(held, await Task.WhenAny(second, held));
            Assert.False(first.IsCompleted);
            release.TrySetResult(true);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        } finally {
            release.TrySetResult(true);
            try { await pending; } catch (Exception) { }
            await first;
            await second;
            await client.DisposeAsync();
        }
    }

    /// <summary>Closing the pool prevents a new connection attempt.</summary>
    [Fact]
    public async Task ClosedPoolRejectsWorkBeforeCallingTheConnectionFactory() {
        var pool = new DnsQuicConnectionPool();
        await pool.DisposeAsync();
        int calls = 0;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pool.GetAsync("endpoint", new QuicClientConnectionOptions(), (_, _) => {
            Interlocked.Increment(ref calls);
            return ValueTask.FromException<QuicConnection>(new InvalidOperationException("Factory must not be admitted."));
        }, default).AsTask());
        Assert.Equal(0, calls);
    }

    /// <summary>Disposal cancels and drains an admitted connection attempt.</summary>
    [Fact]
    public async Task ClosingPoolCancelsAndDrainsAnActiveConnectionFactory() {
        var pool = new DnsQuicConnectionPool();
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task pending = pool.GetAsync("endpoint", new QuicClientConnectionOptions(), async (_, token) => {
            entered.TrySetResult(true);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { drained.TrySetResult(true); }
            throw new InvalidOperationException("Canceled connection must not be published.");
        }, caller.Token).AsTask();
        Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(5000)));
        Task closing = pool.DisposeAsync().AsTask();
        try {
            Assert.Same(closing, await Task.WhenAny(closing, Task.Delay(3000)));
            await closing;
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            Assert.True(drained.Task.IsCompleted);
        } finally {
            caller.Cancel();
            try { await pending; } catch (Exception) { }
            await closing;
        }
    }

    /// <summary>Same-endpoint waiters are rejected and concurrent disposers wait for the same drain.</summary>
    [Fact]
    public async Task ClosingPoolRejectsQueuedWaitersAndSharesItsDrain() {
        var pool = new DnsQuicConnectionPool();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int secondFactoryCalls = 0;
        Task first = pool.GetAsync("endpoint", new QuicClientConnectionOptions(), async (_, token) => {
            using var registration = token.Register(() => canceled.TrySetResult(true));
            entered.TrySetResult(true);
            await release.Task;
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Canceled connection must not be published.");
        }, default).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task queued = pool.GetAsync("endpoint", new QuicClientConnectionOptions(), (_, _) => {
            Interlocked.Increment(ref secondFactoryCalls);
            return ValueTask.FromException<QuicConnection>(new InvalidOperationException("Queued factory must not be admitted."));
        }, default).AsTask();
        Task closing = pool.DisposeAsync().AsTask();
        try {
            Assert.Same(closing, pool.DisposeAsync().AsTask());
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(closing.IsCompleted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
            Assert.Equal(0, secondFactoryCalls);
            release.TrySetResult(true);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => first);
            await closing.WaitAsync(TimeSpan.FromSeconds(5));
        } finally {
            release.TrySetResult(true);
            try { await first; } catch (Exception) { }
            try { await queued; } catch (Exception) { }
            await closing;
        }
    }
}
#endif
