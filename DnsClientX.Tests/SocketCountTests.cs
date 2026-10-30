using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Ensures repeated successful TCP queries release their owned connections.</summary>
    public class SocketCountTests {
        private static int GetTcpConnectionCount(int port) => IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpConnections().Count(c => c.RemoteEndPoint.Port == port && c.State == TcpState.Established);

        private static async Task<int> RunTcpServerAsync(TcpListener listener, int calls, CancellationToken token) {
            using var registration = token.Register(listener.Stop);
            int closed = 0;
            for (int i = 0; i < calls; i++) {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                using var cancelPeer = token.Register(client.Dispose);
                using NetworkStream stream = client.GetStream();
                var length = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, length, 2, token);
                var query = new byte[(length[0] << 8) | length[1]];
                await TestUtilities.ReadExactlyAsync(stream, query, query.Length, token);
                byte[] response = TestUtilities.CreateResponseFromQuery(query);
                byte[] prefix = { (byte)(response.Length >> 8), (byte)response.Length };
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
                Assert.Equal(0, await stream.ReadAsync(new byte[1], 0, 1, token));
                closed++;
            }
            return closed;
        }

        /// <summary>Five valid replies finish with peer EOF and no retained established connection.</summary>
        [Fact]
        public async Task RepeatedCalls_ShouldNotLeakSockets() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            const int iterations = 5;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task<int> server = RunTcpServerAsync(listener, iterations, cts.Token);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
                int before = GetTcpConnectionCount(port);
                for (int i = 0; i < iterations; i++) {
                    DnsResponse response = await DnsWireResolveTcp.ResolveWireFormatTcp(
                        "127.0.0.1", port, "example.com", DnsRecordType.A,
                        false, false, false, config, cts.Token);
                    Assert.Equal(DnsResponseCode.NoError, response.Status);
                }
                Assert.Equal(iterations, await server);
                Assert.Equal(before, GetTcpConnectionCount(port));
            } finally {
                cts.Cancel();
                listener.Stop();
                try { await server; }
                catch (Exception exception) when (cts.IsCancellationRequested
                    && exception is OperationCanceledException or SocketException or ObjectDisposedException or System.IO.IOException) { }
            }
        }
    }
}
