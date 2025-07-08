using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
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
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();
            byte[] len = new byte[2];
            await stream.ReadAsync(len, 0, 2, token);
            if (BitConverter.IsLittleEndian) Array.Reverse(len);
            int length = BitConverter.ToUInt16(len, 0);
            byte[] buffer = new byte[length];
            await stream.ReadAsync(buffer, 0, length, token);
            await Task.Delay(Timeout.Infinite, token);
            listener.Stop();
        }

        [Fact]
        public async Task SendQueryOverTcp_ShouldTimeoutOnStalledServer() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource();
            var serverTask = RunStallingServerAsync(port, cts.Token);

            var queryBytes = new DnsMessage("example.com", DnsRecordType.A, false).SerializeDnsWireFormat();
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveTcp")!;
            MethodInfo method = type.GetMethod("SendQueryOverTcp", BindingFlags.Static | BindingFlags.NonPublic)!;

            await Assert.ThrowsAsync<TimeoutException>(async () => {
                var task = (Task<byte[]>)method.Invoke(null, new object[] { queryBytes, "127.0.0.1", port, 200, CancellationToken.None })!;
                await task;
            });

            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serverTask);
        }
    }
}
