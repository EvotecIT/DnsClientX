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
#if NET5_0_OR_GREATER
                var receiveTask = udp.ReceiveAsync(token).AsTask();
#else
                var receiveTask = udp.ReceiveAsync();
#endif
                var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
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

            // Give the client a slightly larger timeout so retries have time to
            // fire on slower systems.
            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Port = port,
                TimeOut = 100
            };
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveUdp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatUdp", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, 2, cts.Token })!;
            DnsResponse response = await task;

            int attempts = await serverTask;
            cts.Cancel();

            Assert.Equal(2, attempts);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
        }
    }
}