using System;
using System.Reflection;
using System.Threading.Tasks;
using DnsClientX.PowerShell;
using DnsClientX;
using Xunit;

namespace DnsClientX.Tests {
    public class ExecuteWithRetryCancellationTests {
        [Fact]
        public async Task DelayShouldHonorCancellation() {
            var cmdlet = new CmdletResolveDnsQuery {
                RetryCount = 2,
                RetryDelayMs = 10000
            };

            var transientResponse = new DnsResponse {
                Status = DnsResponseCode.ServerFailure,
                Error = "network error"
            };

            Func<Task<DnsResponse[]>> query = () => Task.FromResult(new[] { transientResponse });

            MethodInfo executeWithRetry = typeof(CmdletResolveDnsQuery)
                .GetMethod("ExecuteWithRetry", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var task = (Task<DnsResponse[]>)executeWithRetry.Invoke(cmdlet, new object[] { query })!;

            await Task.Delay(100);

            MethodInfo stopProcessing = typeof(AsyncPSCmdlet)
                .GetMethod("StopProcessing", BindingFlags.NonPublic | BindingFlags.Instance)!;
            stopProcessing.Invoke(cmdlet, null);

            await Assert.ThrowsAsync<TaskCanceledException>(async () => await task);
        }
    }
}
