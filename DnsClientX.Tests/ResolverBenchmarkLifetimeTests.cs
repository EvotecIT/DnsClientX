using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests;

/// <summary>Protects cleanup and bounded execution when benchmark consumers fail.</summary>
public class ResolverBenchmarkLifetimeTests {
    /// <summary>A failing progress callback must not strand attempts waiting for admission.</summary>
    [Fact]
    public async Task ProgressFailureStopsQueuedAttemptsWithoutStrandingTheRun() {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ResolverQueryAttemptResult[]> run = ResolverBenchmarkRunner.RunAsync(
            new[] { new ResolverExecutionTarget { BuiltInEndpoint = DnsEndpoint.Cloudflare } },
            new[] { "example.com" }, new[] { DnsRecordType.A }, 8, 1, new ResolverQueryRunOptions(),
            progress: (_, _) => throw new InvalidOperationException("progress failed"),
            builtInOverride: async (_, _, _, token) => {
                started.TrySetResult(true);
                using var registration = token.Register(() => release.TrySetCanceled());
                await release.Task;
                return new ResolverQueryAttemptResult { Response = new DnsResponse { Status = DnsResponseCode.NoError } };
            }, cancellationToken: cancellation.Token);
        try {
            Assert.Same(started.Task, await Task.WhenAny(started.Task, Task.Delay(3000)));
            release.TrySetResult(true);
            Assert.Same(run, await Task.WhenAny(run, Task.Delay(3000)));
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => run);
            Assert.Equal("progress failed", failure.Message);
        } finally {
            cancellation.Cancel(); release.TrySetResult(true);
            try { await run; } catch (Exception) { }
        }
    }

    /// <summary>Warm sessions reuse transport connections while every attempt still reaches the wire.</summary>
    [Theory]
    [InlineData(ResolverQueryConnectionMode.Cold, 8)]
    [InlineData(ResolverQueryConnectionMode.Warm, 1)]
    public async Task ConnectionModeDoesNotCacheAnswersAndDisposesConnections(ResolverQueryConnectionMode mode, int connections) {
        var server = new ResolverBenchmarkTcpFixture();
        try {
            var results = await ResolverBenchmarkRunner.RunAsync(
                Targets(server.Port), new[] { "example.com" }, new[] { DnsRecordType.A }, 8, 1,
                new ResolverQueryRunOptions { ConnectionMode = mode, TimeoutMs = 2000 });
            Assert.Equal(8, server.Queries);
            Assert.Equal(connections, server.Connections);
            Assert.All(results, result => {
                Assert.True(result.Succeeded, result.EffectiveError);
                Assert.Equal(mode, result.ConnectionMode);
                Assert.True(result.Elapsed > TimeSpan.Zero);
                Assert.NotNull(result.TransportElapsed);
                Assert.True(result.Elapsed >= result.TransportElapsed.Value);
            });
            await server.WaitForClosureAsync(connections);
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>A consumer failure closes retained sockets before the failed run returns.</summary>
    [Fact]
    public async Task WarmProgressFailureDisposesConnectionsAndDoesNotAdmitQueuedQueries() {
        var server = new ResolverBenchmarkTcpFixture();
        try {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ResolverBenchmarkRunner.RunAsync(
                Targets(server.Port), new[] { "example.com" }, new[] { DnsRecordType.A }, 8, 1,
                new ResolverQueryRunOptions { ConnectionMode = ResolverQueryConnectionMode.Warm, TimeoutMs = 2000 },
                progress: (_, _) => throw new InvalidOperationException("progress failed")));
            Assert.Equal(1, server.Queries);
            await server.WaitForClosureAsync(1);
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>Caller cancellation propagates and releases the session's connections.</summary>
    [Fact]
    public async Task WarmCancellationDisposesConnections() {
        using var cancellation = new CancellationTokenSource();
        var server = new ResolverBenchmarkTcpFixture();
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ResolverBenchmarkRunner.RunAsync(
                Targets(server.Port), new[] { "example.com" }, new[] { DnsRecordType.A }, 8, 1,
                new ResolverQueryRunOptions { ConnectionMode = ResolverQueryConnectionMode.Warm, TimeoutMs = 2000 },
                progress: (_, _) => cancellation.Cancel(), cancellationToken: cancellation.Token));
            Assert.Equal(1, server.Queries);
            await server.WaitForClosureAsync(1);
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>DNSSEC child lookups belong to request duration rather than the primary transport exchange.</summary>
    [Fact]
    public async Task TransportMetricExcludesDnsSecMaterialQueries() {
        var server = new ResolverBenchmarkTcpFixture { MaterialDelayMs = 200 };
        try {
            var results = await ResolverBenchmarkRunner.RunAsync(Targets(server.Port), new[] { "example.com" },
                new[] { DnsRecordType.A }, 1, 1, new ResolverQueryRunOptions { ValidateDnsSec = true, TimeoutMs = 2000 });
            var result = Assert.Single(results);
            Assert.True(server.Queries > 1, "Validation must perform actual child DNS queries.");
            Assert.NotNull(result.TransportElapsed);
            Assert.True(result.Elapsed - result.TransportElapsed.Value >= TimeSpan.FromMilliseconds(150),
                "Transport elapsed must exclude the deliberately delayed DNSSEC material lookup.");
        } finally { await server.DisposeAsync(); }
    }

    /// <summary>Cancellation drains simultaneous warm clients without admitting the remaining matrix.</summary>
    [Fact]
    public async Task WarmCancellationClosesAllConcurrentConnections() {
        var server = new ResolverBenchmarkTcpFixture { HoldResponses = true };
        using var cancellation = new CancellationTokenSource();
        var targets = Enumerable.Range(0, 20).Select(index => new ResolverExecutionTarget {
            DisplayName = "loopback-" + index, ExplicitEndpoint = Targets(server.Port)[0].ExplicitEndpoint
        }).ToArray();
        var run = ResolverBenchmarkRunner.RunAsync(targets, new[] { "example.com" },
            new[] { DnsRecordType.A }, 1, 3,
            new ResolverQueryRunOptions { ConnectionMode = ResolverQueryConnectionMode.Warm, TimeoutMs = 2000 },
            cancellationToken: cancellation.Token);
        try {
            await server.WaitForQueriesAsync(3);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.Equal(3, server.Queries);
            Assert.Equal(3, server.Connections);
            server.ReleaseResponses();
            await server.WaitForClosureAsync(3);
        } finally {
            cancellation.Cancel(); server.ReleaseResponses();
            try { await run; } catch (OperationCanceledException) { }
            await server.DisposeAsync();
        }
    }

    /// <summary>Adapters carry the execution mode through public report builders too.</summary>
    [Fact]
    public async Task WarmAdapterObservationsUseRequestedMode() {
        var results = await ResolverBenchmarkRunner.RunAsync(
            new[] { new ResolverExecutionTarget { DisplayName = "adapter", BuiltInEndpoint = DnsEndpoint.Cloudflare } },
            new[] { "example.com" }, new[] { DnsRecordType.A }, 1, 1,
            new ResolverQueryRunOptions { ConnectionMode = ResolverQueryConnectionMode.Warm },
            builtInOverride: (_, _, _, _) => Task.FromResult(new ResolverQueryAttemptResult {
                Target = "adapter", Response = new DnsResponse { Status = DnsResponseCode.NoError }
            }));
        Assert.Equal(ResolverQueryConnectionMode.Warm, results[0].ConnectionMode);
        var report = ResolverBenchmarkReportBuilder.Build(results, new[] { "example.com" }, new[] { DnsRecordType.A },
            1, 1, 1000, new ResolverBenchmarkPolicy());
        Assert.Equal(ResolverQueryConnectionMode.Warm, report.Summary.ConnectionMode);
    }

    private static ResolverExecutionTarget[] Targets(int port) => new[] {
        new ResolverExecutionTarget { DisplayName = "loopback", ExplicitEndpoint = new DnsResolverEndpoint {
            Host = "127.0.0.1", Port = port, Transport = Transport.Tcp, AllowTcpFallback = false
        } }
    };
}
