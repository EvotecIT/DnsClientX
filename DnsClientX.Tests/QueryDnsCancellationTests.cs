using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests cancellation behavior for the <see cref="ClientX.QueryDns"/> method.
    /// </summary>
    public class QueryDnsCancellationTests {
        /// <summary>
        /// Cancels a DNS query before execution and expects a cancelled task.
        /// </summary>
        [Fact]
        public async Task QueryDns_RootServer_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => ClientX.QueryDns("example.com", DnsRecordType.A, DnsEndpoint.RootServer, cancellationToken: cts.Token));
        }
    }
}
