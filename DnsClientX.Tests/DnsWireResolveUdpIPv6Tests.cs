using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for UDP DNS resolution over IPv6.
    /// </summary>
    [Collection("NoParallel")]
    public class DnsWireResolveUdpIPv6Tests {
        private static int GetFreePort() {
            using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private static async Task RunUdpServerAsync(int port, CancellationToken token) {
            using var udp = new UdpClient(AddressFamily.InterNetworkV6);
            udp.Client.DualMode = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.IPv6Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token).AsTask();
#else
            UdpReceiveResult result = await udp.ReceiveAsync();
#endif
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
        }

        /// <summary>
        /// Verifies UDP resolution works with IPv6 servers.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldWorkWithIPv6Server() {
            if (!Socket.OSSupportsIPv6) {
                return;
            }

            int port = GetFreePort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, cts.Token);
            using var pool = new DnsUdpClientPool();

            var config = new Configuration("::1", DnsRequestFormat.DnsOverUDP) { Port = port };
            DnsResponse dnsResponse = await DnsWireResolveUdp.ResolveWireFormatUdp(
                "::1",
                port,
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                debug: false,
                config,
                1,
                cts.Token,
                pool);

            await udpTask;
            Assert.Equal(DnsResponseCode.NoError, dnsResponse.Status);
        }

        /// <summary>
        /// Ensures an invalid server produces a server failure response.
        /// </summary>
        [RealDnsFact]
        public async Task ResolveWireFormatUdp_ShouldReturnServerFailure_ForInvalidServer() {
            var config = new Configuration("invalid", DnsRequestFormat.DnsOverUDP);
            DnsResponse response = await DnsWireResolveUdp.ResolveWireFormatUdp(
                "invalid",
                53,
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                debug: false,
                config,
                1,
                CancellationToken.None);
            Assert.Equal(DnsResponseCode.ServerFailure, response.Status);
            Assert.Contains(response.ErrorCode, new[] { DnsQueryErrorCode.Network, DnsQueryErrorCode.Timeout });
            Assert.False(string.IsNullOrWhiteSpace(response.Error));
            if (response.ErrorCode == DnsQueryErrorCode.Timeout) Assert.IsType<TimeoutException>(response.Exception);
        }
    }
}
