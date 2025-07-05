using System;
using System.Diagnostics;
using System.Reflection;
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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 1, null, false })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ShouldDelayBetweenRetries() {
            int attempts = 0;
            long[] times = new long[3];
            var sw = Stopwatch.StartNew();
            Func<Task<int>> action = () => {
                times[attempts] = sw.ElapsedMilliseconds;
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> Invoke() {
                var generic = method.MakeGenericMethod(typeof(int));
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 50, null, false })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            Assert.Equal(3, attempts);

            var firstInterval = times[1] - times[0];
            var secondInterval = times[2] - times[1];

            Assert.InRange(firstInterval, 40, 500);
            Assert.InRange(secondInterval, 80, 1000);
        }

        [Fact]
        public async Task ShouldUseExponentialBackoff() {
            int attempts = 0;
            long[] times = new long[3];
            var sw = Stopwatch.StartNew();
            Func<Task<int>> action = () => {
                times[attempts] = sw.ElapsedMilliseconds;
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> Invoke() {
                var generic = method.MakeGenericMethod(typeof(int));
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 50, null, false })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            var firstInterval = times[1] - times[0];
            var secondInterval = times[2] - times[1];
            var ratio = secondInterval / (double)firstInterval;

            // Delay should increase exponentially. Allow broad tolerance to avoid
            // flakiness on slower environments.
            Assert.InRange(firstInterval, 40, 500);
            Assert.InRange(ratio, 1.5, 3.0);
        }

        [Fact]
        public async Task ShouldThrowDnsClientExceptionOnTransientResponse() {
            var transientResponse = new DnsResponse { Status = DnsResponseCode.ServerFailure };
            Func<Task<DnsResponse>> action = () => Task.FromResult(transientResponse);

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<DnsResponse> Invoke() {
                var generic = method.MakeGenericMethod(typeof(DnsResponse));
                return (Task<DnsResponse>)generic.Invoke(null, new object[] { action, 2, 1, null, false })!;
            }

            var ex = await Assert.ThrowsAsync<DnsClientException>(Invoke);
            Assert.Equal(DnsResponseCode.ServerFailure, ex.Response.Status);
        }
    }
}
