using System;
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
                return (Task<int>)generic.Invoke(null, new object[] { action, 3, 1 })!;
            }

            await Assert.ThrowsAsync<TimeoutException>(Invoke);
            Assert.Equal(3, attempts);
        }
    }
}

