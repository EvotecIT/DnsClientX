using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>Tests UDP retry bounds and the lifetime of every timed-out attempt.</summary>
    public class DnsWireUdpRetryLimitTests {
        private static async Task<(DnsResponse Response, int[] Ports)> CaptureAttemptsAsync() {
            using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
            using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(guard.Token);
            using var registration = stop.Token.Register(server.Dispose);
            async Task<int[]> ReceiveAsync() {
                var ports = new List<int>();
                try {
                    while (true) {
                        var packet = await server.ReceiveAsync();
                        ports.Add(packet.RemoteEndPoint.Port);
                    }
                } catch (Exception exception) when (stop.IsCancellationRequested
                    && exception is ObjectDisposedException or SocketException) {
                    return ports.ToArray();
                }
            }
            Task<int[]> receive = ReceiveAsync();
            try {
                var configuration = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                    Port = port, TimeOut = 100
                };
                DnsResponse response = await DnsWireResolveUdp.ResolveWireFormatUdp(
                    "127.0.0.1", port, "example.com", DnsRecordType.A,
                    false, false, false, configuration, 2, guard.Token);
                // Keep observing after completion: a third packet must not disappear behind
                // a receiver that stopped as soon as the expected two attempts arrived.
                await Task.Delay(100, guard.Token);
                stop.Cancel();
                return (response, await receive);
            } finally {
                stop.Cancel();
                await receive;
            }
        }

        /// <summary>Two configured attempts send exactly two packets and retain timeout diagnostics.</summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldRespectMaxRetries() {
            var (response, ports) = await CaptureAttemptsAsync();
            Assert.Equal(2, ports.Length); // Also rejects a third attempt after resolver completion.
            Assert.NotEqual(DnsResponseCode.NoError, response.Status);
            Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
            Assert.IsType<TimeoutException>(response.Exception);
        }

        /// <summary>Both timed-out attempts release their local bindings before returning.</summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldDisposeClientOnEachRetry() {
            var (response, ports) = await CaptureAttemptsAsync();
            Assert.Equal(2, ports.Length);
            Assert.Equal(DnsQueryErrorCode.Timeout, response.ErrorCode);
            foreach (int port in ports) {
                using var replacement = new UdpClient(AddressFamily.InterNetwork);
                replacement.Client.ExclusiveAddressUse = true;
                replacement.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
            }
        }
    }
}
