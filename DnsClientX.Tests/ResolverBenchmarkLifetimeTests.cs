using System;
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
}
