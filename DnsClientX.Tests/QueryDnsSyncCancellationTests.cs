using System.Threading;
using Xunit;
using DnsClientX;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests cancellation behavior for the synchronous <see cref="ClientX.QueryDnsSync"/> method.
    /// </summary>
    public class QueryDnsSyncCancellationTests {
        /// <summary>
        /// Cancels a synchronous DNS query prior to execution and expects an exception.
        /// </summary>
        [Fact]
        public void QueryDnsSync_RootServer_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<TaskCanceledException>(() => ClientX.QueryDnsSync("example.com", DnsRecordType.A, DnsEndpoint.RootServer, cancellationToken: cts.Token));
        }
    }
}
