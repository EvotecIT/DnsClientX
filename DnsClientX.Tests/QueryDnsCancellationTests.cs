using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests cancellation behavior for the
    /// <see cref="ClientX.QueryDns(string,DnsRecordType,DnsEndpoint,DnsSelectionStrategy,int,bool,int,int,bool,bool,bool,bool,System.Threading.CancellationToken)"/>
    /// method.
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
