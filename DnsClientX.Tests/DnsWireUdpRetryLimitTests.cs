using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsWireUdpRetryLimitTests {
        private static int GetFreePort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<int> RunUdpServerNoReplyAsync(int port, int expected, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            int count = 0;
            while (count < expected && !token.IsCancellationRequested) {
                var receiveTask = udp.ReceiveAsync();
                var completed = await Task.WhenAny(receiveTask, Task.Delay(100, token));
                if (completed == receiveTask) {
                    await receiveTask;
                    count++;
                }
            }
            return count;
        }

        [Fact]
        public async Task ResolveWireFormatUdp_ShouldRespectMaxRetries() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunUdpServerNoReplyAsync(port, 2, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Port = port,
                TimeOut = 10
            };
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveUdp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatUdp", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, 2, cts.Token })!;
            DnsResponse response = await task;

            cts.Cancel();
            int attempts = await serverTask;

            Assert.Equal(2, attempts);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
        }
    }
}
