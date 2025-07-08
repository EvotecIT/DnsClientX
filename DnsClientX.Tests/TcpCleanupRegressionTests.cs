using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    public class TcpCleanupRegressionTests {
        private static bool IsWindows() {
#if NET6_0_OR_GREATER
            return OperatingSystem.IsWindows();
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }
        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task RunClosingServerAsync(int port, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using TcpClient client = await listener.AcceptTcpClientAsync();
            client.Close();
            listener.Stop();
        }

        private static Task<bool> HasOpenTcpConnectionAsync(int port) {
            var connections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            foreach (var c in connections) {
                if (c.LocalEndPoint.Port == port || c.RemoteEndPoint.Port == port) {
                    if (c.State != TcpState.TimeWait && c.State != TcpState.Closed) {
                        return Task.FromResult(true);
                    }
                }
            }

            return Task.FromResult(false);
        }

        [Fact]
        public async Task TcpFailure_ShouldCloseSocket() {

            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunClosingServerAsync(port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port, TimeOut = 200 };
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveTcp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatTcp", BindingFlags.Static | BindingFlags.NonPublic)!;

            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, cts.Token })!;
            await task;
            await serverTask;
            await Task.Delay(200);

            bool hasConnection = await HasOpenTcpConnectionAsync(port);
            if (hasConnection)
            {
                // give the OS some time to clean up TIME_WAIT sockets
                for (int i = 0; i < 20 && hasConnection; i++)
                {
                    await Task.Delay(100);
                    hasConnection = await HasOpenTcpConnectionAsync(port);
                }
            }

            Assert.False(hasConnection);
        }
    }
}
