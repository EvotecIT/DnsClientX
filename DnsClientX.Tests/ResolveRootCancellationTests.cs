using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests cancellation behavior of <see cref="ClientX.ResolveFromRoot"/>.
    /// </summary>
    public class ResolveRootCancellationTests {
        /// <summary>
        /// Cancels before starting and expects the response to indicate failure.
        /// </summary>
        [Fact]
        public async Task ResolveFromRoot_CancelsEarly() {
            using var client = new ClientX();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var response = await client.ResolveFromRoot("example.com", cancellationToken: cts.Token);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
            Assert.NotNull(response.Error);
        }
    }
}
