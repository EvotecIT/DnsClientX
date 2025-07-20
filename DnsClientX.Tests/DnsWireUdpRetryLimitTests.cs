using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests UDP retry logic and proper resource disposal.
    /// </summary>
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

        private static async Task<int[]> RunUdpServerCapturePortsAsync(int port, int expected, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            var ports = new int[expected];
            int index = 0;
            while (index < expected && !token.IsCancellationRequested) {
#if NET5_0_OR_GREATER
                var receiveTask = udp.ReceiveAsync(token).AsTask();
#else
                var receiveTask = udp.ReceiveAsync();
#endif
                var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
                if (completed == receiveTask) {
                    var result = await receiveTask;
                    ports[index++] = result.RemoteEndPoint.Port;
                }
            }
            if (index < expected) {
                Array.Resize(ref ports, index);
            }
            return ports;
        }

        /// <summary>
        /// Ensures retry logic respects the configured maximum retry count.
        /// </summary>
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
            DnsResponse response = await DnsWireResolveUdp.ResolveWireFormatUdp(
                "127.0.0.1",
                port,
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                debug: false,
                config,
                2,
                cts.Token);

            int attempts = await serverTask;
            cts.Cancel();

            Assert.Equal(2, attempts);
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
        }

        /// <summary>
        /// Validates that the UDP client is disposed on each retry attempt.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldDisposeClientOnEachRetry() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var serverTask = RunUdpServerCapturePortsAsync(port, 2, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Port = port,
                TimeOut = 100
            };
            await DnsWireResolveUdp.ResolveWireFormatUdp(
                "127.0.0.1",
                port,
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                debug: false,
                config,
                2,
                cts.Token);

            int[] clientPorts = await serverTask;
            cts.Cancel();

            foreach (int clientPort in clientPorts) {
                UdpClient? testClient = null;
                Exception? ex = null;
                try {
                    testClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, clientPort));
                } catch (Exception e) {
                    ex = e;
                } finally {
                    testClient?.Dispose();
                }

                Assert.Null(ex);
            }
        }
    }
}