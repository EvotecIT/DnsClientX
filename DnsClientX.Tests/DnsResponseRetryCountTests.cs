using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsResponseRetryCountTests {
        [Fact]
        public async Task RetryCountIsSetOnSuccess() {
            int call = 0;
            Func<Task<DnsResponse>> action = () => {
                call++;
                if (call < 3)
                {
                    return Task.FromResult(new DnsResponse { Status = DnsResponseCode.ServerFailure });
                }
                return Task.FromResult(new DnsResponse { Status = DnsResponseCode.NoError });
            };

            MethodInfo method = typeof(ClientX).GetMethod("RetryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<DnsResponse> Invoke() {
                var generic = method.MakeGenericMethod(typeof(DnsResponse));
                return (Task<DnsResponse>)generic.Invoke(null, new object[] { action, 3, 1, null, false, CancellationToken.None })!;
            }

            DnsResponse res = await Invoke();
            Assert.Equal(2, res.RetryCount);
        }
    }
}
