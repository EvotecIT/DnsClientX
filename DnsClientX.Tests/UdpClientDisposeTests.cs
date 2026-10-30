using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests disposal behavior of UDP clients.
    /// </summary>
    [Collection("NoParallel")]
    public class UdpClientDisposeTests {
        private static int GetFreePort() {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private static async Task<int> RunUdpServerCapturePortAsync(int port, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            using var registration = token.Register(udp.Dispose);
            UdpReceiveResult result = await udp.ReceiveAsync();
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            IPAddress responseAddress = result.RemoteEndPoint.Address.IsIPv4MappedToIPv6
                ? result.RemoteEndPoint.Address.MapToIPv4()
                : result.RemoteEndPoint.Address;
            await udp.SendAsync(response, response.Length,
                new IPEndPoint(responseAddress, result.RemoteEndPoint.Port));
            return result.RemoteEndPoint.Port;
        }

        /// <summary>
        /// Ensures UDP clients are disposed when processing queries.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldDisposeClient() {
            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerCapturePortAsync(port, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port };
            DnsResponse response = await DnsWireResolveUdp.ResolveWireFormatUdp(
                "127.0.0.1",
                port,
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                debug: false,
                config,
                1,
                cts.Token);

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            int clientPort = await udpTask;

            Exception? ex = null;
            for (int attempt = 0; attempt < 10; attempt++) {
                UdpClient? testClient = null;
                try {
                    testClient = new UdpClient(AddressFamily.InterNetwork);
                    testClient.Client.ExclusiveAddressUse = true;
                    testClient.Client.Bind(new IPEndPoint(IPAddress.Loopback, clientPort));
                    ex = null;
                    break;
                } catch (SocketException e) {
                    ex = e;
                    await Task.Delay(50, cts.Token);
                } finally {
                    testClient?.Dispose();
                }
            }

            Assert.Null(ex);
        }
    }
}
