using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class SocketCountTests {
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

        private static int GetTcpConnectionCount() {
            var ip = IPGlobalProperties.GetIPGlobalProperties();
            return ip.GetActiveTcpConnections().Length;
        }

        private static async Task RunTcpServerAsync(int port, int calls, CancellationToken token) {
            byte[] response = CreateDnsHeader();
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            for (int i = 0; i < calls; i++) {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                NetworkStream stream = client.GetStream();
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
            }
            listener.Stop();
        }

        [Fact]
        public async Task RepeatedCalls_ShouldNotLeakSockets() {
            int port = GetFreePort();
            const int iterations = 5;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunTcpServerAsync(port, iterations, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveTcp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatTcp", BindingFlags.Static | BindingFlags.NonPublic)!;

            int before = GetTcpConnectionCount();
            for (int i = 0; i < iterations; i++) {
                var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, cts.Token })!;
                await task;
            }
            await serverTask;

            // allow sockets to close
            await Task.Delay(200);
            int after = GetTcpConnectionCount();
            Assert.InRange(after, before - 1, before + 1);
        }
    }
}
