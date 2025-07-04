using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class TcpClientDisposeTests {
        private class TrackingTcpClient : TcpClient {
            public int DisposeCount { get; private set; }
            protected override void Dispose(bool disposing) {
                base.Dispose(disposing);
                DisposeCount++;
            }
        }

        private static int GetFreePort() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunSilentServerAsync(int port, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using TcpClient client = await listener.AcceptTcpClientAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            listener.Stop();
        }

        [Fact]
        public async Task SendQueryOverTcp_ShouldDisposeClientOnTimeout() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var serverTask = RunSilentServerAsync(port, cts.Token);

            TrackingTcpClient? tracking = null;
            var originalFactory = typeof(ClientX).Assembly
                .GetType("DnsClientX.DnsWireResolveTcp")!
                .GetProperty("TcpClientFactory", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
            var previous = (Func<TcpClient>)originalFactory.GetValue(null)!;
            originalFactory.SetValue(null, new Func<TcpClient>(() => tracking = new TrackingTcpClient()));

            try {
                Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveTcp")!;
                MethodInfo method = type.GetMethod("SendQueryOverTcp", BindingFlags.Static | BindingFlags.NonPublic)!;
                var task = (Task<byte[]>)method.Invoke(null, new object[] { new byte[1], "127.0.0.1", port, 100, CancellationToken.None })!;
                await Assert.ThrowsAsync<TimeoutException>(async () => await task);
            } finally {
                originalFactory.SetValue(null, previous);
                cts.Cancel();
                await serverTask;
            }

            Assert.NotNull(tracking);
            Assert.Equal(1, tracking!.DisposeCount);
        }
    }
}

