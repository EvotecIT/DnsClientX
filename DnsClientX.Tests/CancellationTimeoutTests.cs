using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests verifying that cancellation tokens respect their timeout.
    /// </summary>
    public class CancellationTimeoutTests {
        /// <summary>
        /// Ensures that tasks are cancelled when the token times out.
        /// </summary>
        [Fact]
        public async Task TasksCancelAfterTimeout() {
            using var cts = new CancellationTokenSource(100);
            var tasks = new[] {
                Task.Delay(1000, cts.Token),
                Task.Delay(1000, cts.Token)
            };
            await Assert.ThrowsAsync<TaskCanceledException>(async () => await Task.WhenAll(tasks));
        }
    }
}
