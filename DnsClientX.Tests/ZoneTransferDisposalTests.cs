using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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
            MethodInfo method = typeof(ClientX).GetMethod(
                "SendAxfrOverTcp",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var acceptTask = listener.AcceptTcpClientAsync();

            // Keep caller cancellation as a generous deadlock guard so the deliberately shorter
            // protocol timeout remains the observable result even on a busy CI runner.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
            var enumerable = (IAsyncEnumerable<ZoneTransferResult>)method.Invoke(null, new object[] {
                new byte[] { 0, 0 }, (ushort)0, "example.com", "127.0.0.1", port, 200, false, config, false, cts.Token
            })!;
            var callTask = Task.Run(async () => {
                await foreach (var _ in enumerable) { }
            });

            TcpClient serverClient = await acceptTask;
            var buf = new byte[4];
            int read = 0;
            var stream = serverClient.GetStream();
            while (read < 4) {
                int r = await stream.ReadAsync(buf, read, 4 - read, cts.Token);
                if (r == 0) break;
                read += r;
            }

            await Assert.ThrowsAsync<TimeoutException>(async () => await callTask);

            await Task.Delay(100);
            serverClient.ReceiveTimeout = 200;
            int bytes;
            try {
                bytes = serverClient.Client.Receive(new byte[1]);
            } catch (SocketException) {
                bytes = 0;
            } finally {
                serverClient.Close();
            }

            Assert.Equal(0, bytes);
            listener.Stop();
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
