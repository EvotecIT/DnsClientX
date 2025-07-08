using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    public class ZoneTransferSyncCancellationTests {
        [Fact]
        public void ZoneTransferSync_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            Assert.Throws<TaskCanceledException>(() => client.ZoneTransferSync("example.com", cancellationToken: cts.Token));
        }
    }
}
