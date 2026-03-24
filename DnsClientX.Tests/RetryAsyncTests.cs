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
                return (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 1, null!, null, false, CancellationToken.None })!;
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
                return (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 50, beforeRetry, null, false, CancellationToken.None })!;
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
                return (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 50, beforeRetry, null, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            delays.Add(sw.ElapsedMilliseconds);

            Assert.Equal(3, attempts);

            Assert.Equal(2, delays.Count);

            // With jitter disabled the retry helper uses delayMs << attempt,
            // so the two waits should clear progressively higher minimums even
            // if the host oversleeps under load.
            Assert.InRange(delays[0], 80, 5000);
            Assert.InRange(delays[1], 160, 10000);
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
                return (Task<DnsResponse>)generic.Invoke(null!, new object?[] { action, 2, 1, null!, null, false, CancellationToken.None })!;
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

            Task<int> task = (Task<int>)generic.Invoke(null!, new object?[] { action, 3, 5000, null!, null, false, cts.Token })!;
            cts.CancelAfter(200);

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
            Assert.Equal(1, attempts);
        }
    }
}
