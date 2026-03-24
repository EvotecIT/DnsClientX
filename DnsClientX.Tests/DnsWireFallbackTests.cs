using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests fallback behavior from UDP to TCP queries.
    /// </summary>
    public class DnsWireFallbackTests {
        private static int GetFreeUdpPort() {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private static byte[] CreateDnsHeader(bool truncated) {
            byte[] bytes = new byte[12];
            ushort id = 0x1234;
            bytes[0] = (byte)(id >> 8);
            bytes[1] = (byte)(id & 0xFF);
            ushort flags = truncated ? (ushort)0x0200 : (ushort)0x8180;
            bytes[2] = (byte)(flags >> 8);
            bytes[3] = (byte)(flags & 0xFF);
            // remaining bytes left at 0 (counts)
            return bytes;
        }

        private static async Task RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token).AsTask();
#else
            UdpReceiveResult result = await udp.ReceiveAsync();
#endif
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
        }

        private static async Task RunTcpServerAsync(TcpListener listener, byte[] response, Action onReceived, CancellationToken token) {
            try {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                NetworkStream stream = client.GetStream();
                byte[] lengthBuffer = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, lengthBuffer, 2, token);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
                int length = BitConverter.ToUInt16(lengthBuffer, 0);
                byte[] queryBuffer = new byte[length];
                await TestUtilities.ReadExactlyAsync(stream, queryBuffer, length, token);
                onReceived();
                byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
            } finally {
                listener.Stop();
            }
        }

        /// <summary>
        /// UDP queries should automatically retry over TCP when truncated.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldFallbackToTcpWhenTruncated() {
            var udpResponse = CreateDnsHeader(truncated: true);
            var tcpResponse = CreateDnsHeader(truncated: false);
            bool tcpCalled = false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var udpTask = RunUdpServerAsync(port, udpResponse, cts.Token);
            var tcpTask = RunTcpServerAsync(listener, tcpResponse, () => tcpCalled = true, cts.Token);

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

            await Task.WhenAll(udpTask, tcpTask);
            Assert.True(tcpCalled, "Expected TCP fallback to be used");
            Assert.False(response.IsTruncated);
            Assert.Equal(Transport.Tcp, response.UsedTransport);
        }

        /// <summary>
        /// Ensures TCP fallback can be disabled for UDP queries.
        /// </summary>
        [Fact]
        public async Task ResolveWireFormatUdp_ShouldNotFallbackWhenDisabled() {
            int port = GetFreeUdpPort();
            var udpResponse = CreateDnsHeader(truncated: true);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, udpResponse, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port, UseTcpFallback = false };
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

            await udpTask;
            Assert.True(response.IsTruncated);
            Assert.Equal(Transport.Udp, response.UsedTransport);
        }
    }
}
