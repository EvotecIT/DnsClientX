using System;
using System.Diagnostics;
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

        private static async Task<bool> NetstatHasPortAsync(int port) {
            string args = IsWindows() ? "-ano" : "-an";
            using Process proc = new Process();
            proc.StartInfo.FileName = "netstat";
            proc.StartInfo.Arguments = args;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
            string output = await proc.StandardOutput.ReadToEndAsync();
            proc.WaitForExit();
            return output.Contains($":{port}");
        }

        [Fact]
        public async Task TcpFailure_ShouldCloseSocket() {
            if (!IsWindows() && !File.Exists("/bin/netstat") && !File.Exists("/usr/bin/netstat")) {
                return; // skip if netstat is not available
            }

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

            bool hasConnection = await NetstatHasPortAsync(port);
            if (hasConnection)
            {
                // give the OS some time to clean up TIME_WAIT sockets
                for (int i = 0; i < 20 && hasConnection; i++)
                {
                    await Task.Delay(100);
                    hasConnection = await NetstatHasPortAsync(port);
                }
            }

            Assert.False(hasConnection);
        }
    }
}
