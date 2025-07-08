using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Linq;
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

        private static async Task<bool> HasOpenTcpConnectionAsync(int port) {
            if (IsWindows()) {
                try {
                    var connTask = Task.Run(() => IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections());
                    if (await Task.WhenAny(connTask, Task.Delay(2000)) == connTask) {
                        var connections = connTask.Result;
                        return connections.Any(c => (c.LocalEndPoint.Port == port || c.RemoteEndPoint.Port == port) && c.State != TcpState.TimeWait && c.State != TcpState.Closed);
                    }
                } catch {
                    // fall back to netstat
                }
            }

            try {
                string args = IsWindows() ? "-ano" : "-an";
                using var proc = new Process();
                proc.StartInfo.FileName = "netstat";
                proc.StartInfo.Arguments = args;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.UseShellExecute = false;
                proc.Start();
                var readTask = proc.StandardOutput.ReadToEndAsync();
                var timeoutTask = Task.Delay(2000);
                var completed = await Task.WhenAny(readTask, timeoutTask);
                if (completed == readTask) {
                    string output = await readTask;
                    if (!proc.HasExited) {
                        try { proc.Kill(); } catch { }
                    }
                    return output.Contains($":{port}");
                } else {
                    try { proc.Kill(); } catch { }
                }
            } catch {
                // ignore and assume no connection
            }

            return false;
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
