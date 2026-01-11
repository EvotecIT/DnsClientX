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
    public class DnsWireResolveUdpIPv6Tests {
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
            using var socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.IPv6Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private static async Task RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(AddressFamily.InterNetworkV6);
            udp.Client.DualMode = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.IPv6Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token).AsTask();
#else
            UdpReceiveResult result = await udp.ReceiveAsync();
#endif
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
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, response, cts.Token);

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
                cts.Token);

            await udpTask;
            Assert.Equal(DnsResponseCode.NoError, dnsResponse.Status);
        }

        /// <summary>
        /// Ensures an invalid server produces a server failure response.
        /// </summary>
        [Fact]
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
            var error = response.Error ?? string.Empty;
            var isInvalid = error.IndexOf("invalid dns server", StringComparison.OrdinalIgnoreCase) >= 0;
            var isTimeout = error.IndexOf("resolution timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0;
            Assert.True(isInvalid || isTimeout, $"Unexpected error: '{error}'");
        }
    }
}
