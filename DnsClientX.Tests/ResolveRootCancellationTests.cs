using System.Net;
using System.Net.Sockets;
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

        /// <summary>
        /// Ensures cancellation prevents additional root-server queries once triggered during resolution.
        /// </summary>
        [Fact]
        public async Task ResolveFromRoot_ShouldStopAfterCancellation() {
            using var client = new ClientX();
            using var cts = new CancellationTokenSource();

            int port = TestUtilities.GetFreePort();
            using var firstServer = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            using var secondServer = new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.2"), port));

            var firstReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _ = Task.Run(async () => {
                try {
                    await firstServer.ReceiveAsync();
                    firstReceived.TrySetResult(true);
                    cts.Cancel();
                } catch (ObjectDisposedException) {
                    firstReceived.TrySetResult(false);
                }
            });

            _ = Task.Run(async () => {
                try {
                    await secondServer.ReceiveAsync();
                    secondReceived.TrySetResult(true);
                } catch (ObjectDisposedException) {
                    secondReceived.TrySetResult(false);
                }
            });

            var resolveTask = client.ResolveFromRoot(
                "example.com",
                servers: new[] { "127.0.0.1", "127.0.0.2" },
                maxRetries: 2,
                port: port,
                cancellationToken: cts.Token);

            await firstReceived.Task;
            cts.Cancel();

            var response = await resolveTask;
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
            Assert.NotNull(response.Error);

            var secondCompleted = await Task.WhenAny(secondReceived.Task, Task.Delay(500));
            var contactedSecond = secondCompleted == secondReceived.Task && await secondReceived.Task;
            Assert.False(contactedSecond, "Cancellation should stop additional server queries.");
        }
    }
}
