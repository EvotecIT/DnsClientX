using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class RetryAsyncTests {
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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 1, null, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);
            Assert.Equal(3, attempts);
        }

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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 50, beforeRetry, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            delays.Add(sw.ElapsedMilliseconds);

            Assert.Equal(3, attempts);

            Assert.Equal(2, delays.Count);

            Assert.InRange(delays[0], 40, 1000);
            Assert.InRange(delays[1], 80, 1500);
        }

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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 50, beforeRetry, false, CancellationToken.None })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            delays.Add(sw.ElapsedMilliseconds);

            Assert.Equal(3, attempts);

            Assert.Equal(2, delays.Count);

            var ratio = delays[1] / (double)delays[0];

            // Delay should increase exponentially. Allow broad tolerance for slow
            // environments and timer inaccuracies.
            Assert.InRange(delays[0], 40, 1000);
            Assert.InRange(ratio, 1.1, 3.5);
        }

        [Fact]
        public async Task ShouldThrowDnsClientExceptionOnTransientResponse() {
            var transientResponse = new DnsResponse { Status = DnsResponseCode.ServerFailure };
            Func<Task<DnsResponse>> action = () => Task.FromResult(transientResponse);

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<DnsResponse> Invoke() {
                var generic = method.MakeGenericMethod(typeof(DnsResponse));
                return (Task<DnsResponse>)generic.Invoke(null, new object[] { action, 2, 1, null, false, CancellationToken.None })!;
            }

            var ex = await Assert.ThrowsAsync<DnsClientException>(Invoke);
            Assert.Equal(DnsResponseCode.ServerFailure, ex.Response.Status);
        }

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

            Task<int> task = (Task<int>)generic.Invoke(null, new object[] { action, 3, 5000, null, false, cts.Token })!;
            cts.CancelAfter(200);

            await Assert.ThrowsAsync<TaskCanceledException>(() => task);
            Assert.Equal(1, attempts);
        }
    }
}
