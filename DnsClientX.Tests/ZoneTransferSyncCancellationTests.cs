using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests cancellation handling for synchronous zone transfers.
    /// </summary>
    public class ZoneTransferSyncCancellationTests {
        /// <summary>
        /// Cancels the zone transfer before it begins and expects a cancelled task.
        /// </summary>
        [Fact]
        public void ZoneTransferSync_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            Assert.Throws<TaskCanceledException>(() => client.ZoneTransferSync("example.com", cancellationToken: cts.Token));
        }
    }
}
