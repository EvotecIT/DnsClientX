using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests cancellation behavior of <see cref="ClientX.ResolveFromRoot"/>.
    /// </summary>
    public class ResolveRootCancellationTests {
        /// <summary>
        /// Cancels before starting and expects caller cancellation to propagate.
        /// </summary>
        [Fact]
        public async Task ResolveFromRoot_CancelsEarly() {
            using var client = new ClientX();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.ResolveFromRoot("example.com", cancellationToken: cts.Token));
        }
    }
}
