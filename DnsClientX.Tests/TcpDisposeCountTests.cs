using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    [Collection("DisposalTests")]
    public class TcpDisposeCountTests {
        private class CountingTcpClient : TcpClient {
            private readonly Action _onDispose;
            private volatile int _disposeCount = 0;
            public CountingTcpClient(Action onDispose) => _onDispose = onDispose;
            protected override void Dispose(bool disposing) {
                if (Interlocked.CompareExchange(ref _disposeCount, 1, 0) == 0) {
                    base.Dispose(disposing);
                    _onDispose();
                }
            }
        }

        private static byte[] CreateDnsHeader() {
            byte[] bytes = new byte[12];
            ushort id = 0x1234;
            bytes[0] = (byte)(id >> 8);
            bytes[1] = (byte)(id & 0xFF);
            ushort flags = 0x8180;
            bytes[2] = (byte)(flags >> 8);
            bytes[3] = (byte)(flags & 0xFF);
            return bytes;
        }

        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunTcpServerAsync(int port, CancellationToken token) {
            byte[] response = CreateDnsHeader();
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using TcpClient client = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = client.GetStream();
            byte[] len = new byte[2];
            await stream.ReadAsync(len, 0, 2, token);
            if (BitConverter.IsLittleEndian) Array.Reverse(len);
            int length = BitConverter.ToUInt16(len, 0);
            byte[] buffer = new byte[length];
            await stream.ReadAsync(buffer, 0, length, token);
            byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
            await stream.WriteAsync(prefix, 0, prefix.Length, token);
            await stream.WriteAsync(response, 0, response.Length, token);
            listener.Stop();
        }

        [Fact]
        public async Task ResolveWireFormatTcp_ShouldDisposeConnection() {
            int disposed = 0;
            var prevFactory = DnsWireResolveTcp.TcpClientFactory;

            // Use a lock to prevent parallel test interference
            var lockObj = new object();

            try {
                lock (lockObj) {
                    DnsWireResolveTcp.TcpClientFactory = () => new CountingTcpClient(() => {
                        lock (lockObj) {
                            disposed++;
                        }
                    });
                }

                int port = GetFreePort();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var serverTask = RunTcpServerAsync(port, cts.Token);

                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
                Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveTcp")!;
                MethodInfo method = type.GetMethod("ResolveWireFormatTcp", BindingFlags.Static | BindingFlags.NonPublic)!;
                var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, cts.Token })!;
                await task;
                await serverTask;

                int finalDisposed;
                lock (lockObj) {
                    finalDisposed = disposed;
                }
                Assert.Equal(1, finalDisposed);
            } finally {
                // Ensure cleanup happens even if test fails
                DnsWireResolveTcp.TcpClientFactory = prevFactory;

                // Give a small delay to ensure any pending disposals complete
                await Task.Delay(10);
            }
        }
    }
}
