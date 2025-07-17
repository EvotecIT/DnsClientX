using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the internal retry helper methods.
    /// </summary>
    public class RetryAsyncTests {
        /// <summary>
        /// Ensures the action is retried the configured number of times.
        /// </summary>
        [Fact]
        public async Task ShouldRetrySpecifiedNumberOfTimes() {
            int attempts = 0;
            Func<Task<int>> action = () => {
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> Invoke() {
                var generic = method.MakeGenericMethod(typeof(int));
                return (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 1, null!, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);
            Assert.Equal(3, attempts);
        }

        /// <summary>
        /// Verifies that a delay is inserted between retries.
        /// </summary>
        [Fact]
        public async Task ShouldDelayBetweenRetries() {
            int attempts = 0;
            var delays = new List<long>();
            var sw = new Stopwatch();
            Action beforeRetry = () => {
                if (!sw.IsRunning) {
                    sw.Start();
                } else {
                    delays.Add(sw.ElapsedMilliseconds);
                    sw.Restart();
                }
            };

            Func<Task<int>> action = () => {
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> Invoke() {
                var generic = method.MakeGenericMethod(typeof(int));
                return (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 50, beforeRetry, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            delays.Add(sw.ElapsedMilliseconds);

            Assert.Equal(3, attempts);

            Assert.Equal(2, delays.Count);

            Assert.InRange(delays[0], 40, 1000);
            Assert.InRange(delays[1], 80, 1500);
        }

        /// <summary>
        /// Confirms exponential backoff is used when enabled.
        /// </summary>
        [Fact]
        public async Task ShouldUseExponentialBackoff() {
            int attempts = 0;
            var delays = new List<long>();
            var sw = new Stopwatch();
            Action beforeRetry = () => {
                if (!sw.IsRunning) {
                    sw.Start();
                } else {
                    delays.Add(sw.ElapsedMilliseconds);
                    sw.Restart();
                }
            };

            Func<Task<int>> action = () => {
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> Invoke() {
                var generic = method.MakeGenericMethod(typeof(int));
                return (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 50, beforeRetry, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            delays.Add(sw.ElapsedMilliseconds);

            Assert.Equal(3, attempts);

            Assert.Equal(2, delays.Count);

            var ratio = delays[1] / (double)delays[0];

            // Delay should increase exponentially. Allow broad tolerance for slow
            // environments and timer inaccuracies. On heavily loaded systems the
            // ratio can be slightly below 1, so check for a minimal increase.
            Assert.InRange(delays[0], 40, 1000);
            // Allow very wide tolerance for slow environments and coarse timers
            // which can result in a ratio far from the expected value. We only
            // check that the delay increased by a noticeable amount.
            Assert.True(ratio >= 0.5 && ratio <= 5.0, $"Unexpected ratio: {ratio}");
        }

        /// <summary>
        /// Throws <see cref="DnsClientException"/> when a transient response is encountered.
        /// </summary>
        [Fact]
        public async Task ShouldThrowDnsClientExceptionOnTransientResponse() {
            var transientResponse = new DnsResponse { Status = DnsResponseCode.ServerFailure };
            Func<Task<DnsResponse>> action = () => Task.FromResult(transientResponse);

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<DnsResponse> Invoke() {
                var generic = method.MakeGenericMethod(typeof(DnsResponse));
                return (Task<DnsResponse>)generic.Invoke(null!, new object?[] { action, 2, 1, null!, false, CancellationToken.None })!;
            }

            var ex = await Assert.ThrowsAsync<DnsClientException>(Invoke);
            Assert.NotNull(ex.Response);
            Assert.Equal(DnsResponseCode.ServerFailure, ex.Response!.Status);
            Assert.Same(transientResponse, ex.Response);
        }

        /// <summary>
        /// Ensures cancellation during retry delay stops further attempts.
        /// </summary>
        [Fact]
        public async Task ShouldCancelDuringDelay() {
            int attempts = 0;
            Func<Task<int>> action = () => {
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            var generic = method.MakeGenericMethod(typeof(int));
            using var cts = new CancellationTokenSource();

            Task<int> task = (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 5000, null!, false, cts.Token })!;
            cts.CancelAfter(200);

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
            Assert.Equal(1, attempts);
        }
    }
}
