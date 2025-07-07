using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class QueryDnsCancellationTests {
        [Fact]
        public async Task QueryDns_RootServer_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(
                () => ClientX.QueryDns("example.com", DnsRecordType.A, DnsEndpoint.RootServer, cancellationToken: cts.Token));
        }
    }
}
