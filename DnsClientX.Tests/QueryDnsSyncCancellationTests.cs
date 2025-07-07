using System.Threading;
using Xunit;
using DnsClientX;

namespace DnsClientX.Tests {
    public class QueryDnsSyncCancellationTests {
        [Fact]
        public void QueryDnsSync_RootServer_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<TaskCanceledException>(() => ClientX.QueryDnsSync("example.com", DnsRecordType.A, DnsEndpoint.RootServer, cancellationToken: cts.Token));
        }
    }
}
