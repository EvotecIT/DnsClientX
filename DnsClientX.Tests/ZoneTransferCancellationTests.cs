using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for cancellation behavior during zone transfers.
    /// </summary>
    public class ZoneTransferCancellationTests {
        /// <summary>
        /// Ensures cancellation tokens are honored during zone transfers.
        /// </summary>
        [Fact]
        public async Task ZoneTransferAsync_CancelledTask() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverTCP);
            await Assert.ThrowsAsync<TaskCanceledException>(() => client.ZoneTransferAsync("example.com", cancellationToken: cts.Token));
        }
    }
}
