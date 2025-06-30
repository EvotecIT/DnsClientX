using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class EdnsDoBitTests {
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

        private static async Task<byte[]> RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(port);
            UdpReceiveResult result = await udp.ReceiveAsync().ConfigureAwait(false);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint).ConfigureAwait(false);
            return result.Buffer;
        }

        private static async Task<byte[]> RunTcpServerAsync(int port, byte[] response, CancellationToken token) {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            using TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            byte[] lengthBuffer = new byte[2];
            await stream.ReadAsync(lengthBuffer, 0, 2, token).ConfigureAwait(false);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBuffer);
            int length = BitConverter.ToUInt16(lengthBuffer, 0);
            byte[] queryBuffer = new byte[length];
            await stream.ReadAsync(queryBuffer, 0, length, token).ConfigureAwait(false);
            byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(prefix);
            await stream.WriteAsync(prefix, 0, prefix.Length, token).ConfigureAwait(false);
            await stream.WriteAsync(response, 0, response.Length, token).ConfigureAwait(false);
            listener.Stop();
            return queryBuffer;
        }

        private static void AssertDoBit(byte[] query, string name) {
            int additionalCount = (query[10] << 8) | query[11];
            Assert.Equal(1, additionalCount);

            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1;
            offset += 2;
            offset += 2;

            Assert.Equal(0, query[offset]);
            ushort type = (ushort)((query[offset + 1] << 8) | query[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            uint ttl = (uint)((query[offset + 5] << 24) | (query[offset + 6] << 16) | (query[offset + 7] << 8) | query[offset + 8]);
            Assert.Equal(0x00008000u, ttl);
        }

        [Fact]
        public async Task UdpRequest_ShouldIncludeDoBit_WhenRequested() {
            int port = GetFreePort();
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, response, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port };
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveUdp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatUdp", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, true, false, false, config, cts.Token })!;
            await task.ConfigureAwait(false);
            byte[] query = await udpTask.ConfigureAwait(false);

            AssertDoBit(query, "example.com");
        }

        [Fact]
        public async Task TcpRequest_ShouldIncludeDoBit_WhenRequested() {
            int port = GetFreePort();
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var tcpTask = RunTcpServerAsync(port, response, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port };
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveTcp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatTcp", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, true, false, false, config, cts.Token })!;
            await task.ConfigureAwait(false);
            byte[] query = await tcpTask.ConfigureAwait(false);

            AssertDoBit(query, "example.com");
        }
    }
}
