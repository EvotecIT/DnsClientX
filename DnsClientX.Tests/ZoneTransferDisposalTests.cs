using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests disposal of resources when performing zone transfers.
    /// </summary>
    [Collection("NoParallel")]
    public class ZoneTransferDisposalTests {
        /// <summary>
        /// Verifies resources are disposed when AXFR over TCP times out.
        /// </summary>
        [Fact]
        public async Task SendAxfrOverTcp_ShouldDisposeResources_OnTimeout() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync();

            // Keep caller cancellation as a generous deadlock guard so the deliberately shorter
            // protocol timeout remains the observable result even on a busy CI runner.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var stopListener = cts.Token.Register(listener.Stop);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port, TimeOut = 200 };
                using var client = new ClientX(config);
                Task<ZoneTransferResult[]> callTask = client.ZoneTransferAsync("example.com", retryOnTransient: false,
                    cancellationToken: cts.Token);

                using TcpClient serverClient = await acceptTask;
                var stream = serverClient.GetStream();
                byte[] prefix = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, prefix, 2, cts.Token);
                byte[] query = new byte[(prefix[0] << 8) | prefix[1]];
                await TestUtilities.ReadExactlyAsync(stream, query, query.Length, cts.Token);

                DnsClientException timeout = await Assert.ThrowsAsync<DnsClientException>(async () => await callTask);
                Assert.IsType<TimeoutException>(timeout.InnerException);

                await Task.Delay(100);
                serverClient.ReceiveTimeout = 200;
                int bytes;
                try {
                    bytes = serverClient.Client.Receive(new byte[1]);
                } catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted) {
                    bytes = 0;
                }

                Assert.Equal(0, bytes);
            } finally {
                cts.Cancel();
                listener.Stop();
            }
        }

        /// <summary>Transport failures retain their root cause for diagnostics.</summary>
        [Fact]
        public async Task ZoneTransferAsync_PreservesConnectionFailure() {
            Func<AddressFamily, TcpClient> previous = DnsWireResolveTcp.TcpClientFactory;
            try {
                DnsWireResolveTcp.TcpClientFactory = _ =>
                    throw new IOException("Synthetic zone-transfer connection failure.");
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP);
                using var client = new ClientX(configuration);

                DnsClientException exception = await Assert.ThrowsAsync<DnsClientException>(
                    () => client.ZoneTransferAsync("example.com", retryOnTransient: false));

                IOException inner = Assert.IsType<IOException>(exception.InnerException);
                Assert.Contains("Synthetic zone-transfer", inner.Message, StringComparison.Ordinal);
            } finally {
                DnsWireResolveTcp.TcpClientFactory = previous;
            }
        }
    }
}
