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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 1, null })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);
            Assert.Equal(3, attempts);
        }

        [Fact]
        public async Task ShouldDelayBetweenRetries() {
            int attempts = 0;
            Func<Task<int>> action = () => {
                attempts++;
                throw new TimeoutException();
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> Invoke() {
                var generic = method.MakeGenericMethod(typeof(int));
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 50, null })!;
            }

            var sw = Stopwatch.StartNew();
            await Assert.ThrowsAsync<TimeoutException>(Invoke);
            sw.Stop();

            Assert.Equal(3, attempts);
            // Allow a little more headroom for slower environments
            Assert.InRange(sw.ElapsedMilliseconds, 150, 350);
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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 50, null })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);

            var firstInterval = times[1] - times[0];
            var secondInterval = times[2] - times[1];

            // Allow a small margin for OS timer variance
            Assert.True(secondInterval + 5 >= firstInterval);
        }

        [Fact]
        public async Task ShouldThrowDnsClientExceptionOnTransientResponse() {
            var transientResponse = new DnsResponse { Status = DnsResponseCode.ServerFailure };
            Func<Task<DnsResponse>> action = () => Task.FromResult(transientResponse);

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<DnsResponse> Invoke() {
                var generic = method.MakeGenericMethod(typeof(DnsResponse));
                return (Task<DnsResponse>)generic.Invoke(null, new object[] { action, 2, 1, null })!;
            }

            var ex = await Assert.ThrowsAsync<DnsClientException>(Invoke);
            Assert.Equal(DnsResponseCode.ServerFailure, ex.Response.Status);
        }
    }
}
