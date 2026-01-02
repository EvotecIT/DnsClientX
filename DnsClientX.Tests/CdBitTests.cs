using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests that verify the behavior of the Checking Disabled (CD) bit.
    /// </summary>
    public class CdBitTests {
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

        private static int GetFreeTcpPort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static int GetFreeUdpPort() {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private static async Task<byte[]> RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            UdpReceiveResult result = await udp.ReceiveAsync();
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        private static async Task<byte[]> RunTcpServerAsync(int port, byte[] response, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();
            byte[] lengthBuffer = new byte[2];
            await stream.ReadAsync(lengthBuffer, 0, 2, token);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
            int length = BitConverter.ToUInt16(lengthBuffer, 0);
            byte[] queryBuffer = new byte[length];
            await stream.ReadAsync(queryBuffer, 0, length, token);
            byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
            await stream.WriteAsync(prefix, 0, prefix.Length, token);
            await stream.WriteAsync(response, 0, response.Length, token);
            listener.Stop();
            return queryBuffer;
        }

        private static void AssertCdBit(byte[] query, string name, uint expectedTtl) {
            int additionalCount = (query[10] << 8) | query[11];
            Assert.Equal(1, additionalCount);

            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;

            Assert.Equal(0, query[offset]);
            ushort type = (ushort)((query[offset + 1] << 8) | query[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            uint ttl = (uint)((query[offset + 5] << 24) | (query[offset + 6] << 16) | (query[offset + 7] << 8) | query[offset + 8]);
            Assert.Equal(expectedTtl, ttl);
        }

        /// <summary>
        /// Ensures UDP queries set the CD bit when <see cref="Configuration.CheckingDisabled"/> is true.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldIncludeCdBit_WhenConfigured() {
            int port = GetFreeUdpPort();
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, response, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port, CheckingDisabled = true };
            await DnsWireResolveUdp.ResolveWireFormatUdp(
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
            byte[] query = await udpTask;

            AssertCdBit(query, "example.com", 0x10u);
        }

        /// <summary>
        /// Ensures TCP queries set the CD bit when <see cref="Configuration.CheckingDisabled"/> is true.
        /// </summary>
        [Fact]
        public async Task TcpRequest_ShouldIncludeCdBit_WhenConfigured() {
            int port = GetFreeTcpPort();
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var tcpTask = RunTcpServerAsync(port, response, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port, CheckingDisabled = true };
            await DnsWireResolveTcp.ResolveWireFormatTcp(
                "127.0.0.1",
                port,
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                debug: false,
                config,
                cts.Token);
            byte[] query = await tcpTask;

            AssertCdBit(query, "example.com", 0x10u);
        }

        /// <summary>
        /// When DNSSEC validation is requested, the CD bit must also be set.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldIncludeCdBit_WhenValidateDnsSecTrue() {
            int port = GetFreeUdpPort();
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, response, cts.Token);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, useTcpFallback: false);
            client.EndpointConfiguration.Port = port;

            await client.Resolve("example.com", DnsRecordType.A, requestDnsSec: false, validateDnsSec: true, retryOnTransient: false, cancellationToken: cts.Token);

            byte[] query = await udpTask;

            AssertCdBit(query, "example.com", 0x10u);
        }

        /// <summary>
        /// Ensures DOT requests include the CD bit when configured.
        /// </summary>
        [Fact]
        public void DotRequest_ShouldIncludeCdBit_WhenConfigured() {
            var message = new DnsMessage("example.com", DnsRecordType.A, false, true, 4096, null, true, null);
            byte[] data = message.SerializeDnsWireFormat();
            AssertCdBit(data, "example.com", 0x10u);
        }

        /// <summary>
        /// Ensures DOQ requests include the CD bit when configured.
        /// </summary>
        [Fact]
        public void DoqRequest_ShouldIncludeCdBit_WhenConfigured() {
            var message = new DnsMessage("example.com", DnsRecordType.A, false, true, 4096, null, true, null);
            byte[] data = message.SerializeDnsWireFormat();
            AssertCdBit(data, "example.com", 0x10u);
        }
    }
}
