using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests TCP read timeout handling.
    /// </summary>
    public class DnsWireReadTimeoutTests {
        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunStallingServerAsync(int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using var registration = token.Register(listener.Stop);
            try {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                NetworkStream stream = client.GetStream();
                byte[] len = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, len, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(len);
                int length = BitConverter.ToUInt16(len, 0);
                byte[] buffer = new byte[length];
                await TestUtilities.ReadExactlyAsync(stream, buffer, length, token);
                await Task.Delay(Timeout.Infinite, token);
            } catch (Exception exception) when (token.IsCancellationRequested &&
                exception is SocketException or ObjectDisposedException) {
                throw new OperationCanceledException(token);
            } finally {
                listener.Stop();
            }
        }

        private static async Task StopStallingServerAsync(Task server, CancellationTokenSource cancellation) {
            cancellation.Cancel();
            try {
                await server;
            } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
                // The fixture was still waiting for the request or deliberately stalling.
            } catch (IOException exception) when (cancellation.IsCancellationRequested &&
                (exception is EndOfStreamException ||
                 exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset })) {
                // A timed-out client may reset the connection before the server reads its query.
            }
        }

        /// <summary>The response wrapper retains a stalled TCP exchange's timeout diagnostics.</summary>
        [Fact]
        public async Task ResolveWireFormatTcp_PreservesTimeoutDiagnostics() {
            int port = GetFreePort();
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task server = RunStallingServerAsync(port, cancel.Token);
            var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) {
                Port = port, TimeOut = 200
            };
            try {
                DnsResponse response = await DnsWireResolveTcp.ResolveWireFormatTcp("127.0.0.1", port,
                    "timeout.example", DnsRecordType.A, false, false, false, configuration, cancel.Token);

                Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
                Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
                Assert.NotNull(response.Exception);
            } finally {
                await StopStallingServerAsync(server, cancel);
            }
        }

        /// <summary>
        /// Ensures TCP DNS queries time out when the server stalls.
        /// </summary>
        [Fact]
        public async Task SendQueryOverTcp_ShouldTimeoutOnStalledServer() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource();
            var serverTask = RunStallingServerAsync(port, cts.Token);

            var queryBytes = new DnsMessage("example.com", DnsRecordType.A, false).SerializeDnsWireFormat();

            try {
                await Assert.ThrowsAsync<TimeoutException>(async () => {
                    await DnsWireResolveTcp.SendQueryOverTcp(
                        queryBytes,
                        "127.0.0.1",
                        port,
                        200,
                        CancellationToken.None);
                });
            } finally {
                await StopStallingServerAsync(serverTask, cts);
            }
        }
    }
}
