using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for EDNS Client Subnet option handling.
    /// </summary>
    public class EcsOptionTests {
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
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint!).Port;
        }

        private static async Task<byte[]> RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token).AsTask();
#else
            UdpReceiveResult result = await udp.ReceiveAsync();
#endif
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        private static void AssertEcsOption(byte[] query, string name) {
            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2; // end of question

            Assert.Equal(0, query[offset]);
            offset += 1;
            ushort type = (ushort)((query[offset] << 8) | query[offset + 1]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            offset += 2 + 2 + 4; // udp size + ttl
            ushort rdlen = (ushort)((query[offset] << 8) | query[offset + 1]);
            Assert.True(rdlen > 0);
            offset += 2;
            ushort optionCode = (ushort)((query[offset] << 8) | query[offset + 1]);
            Assert.Equal(8, optionCode); // ECS option
        }

        /// <summary>
        /// Ensures that the EDNS Client Subnet option is included when configured.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldIncludeEcsOption_WhenSubnetConfigured() {
            int port = GetFreePort();
            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, response, cts.Token);

            var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                Port = port,
                EnableEdns = true,
                Subnet = "192.0.2.1/24"
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
                1,
                cts.Token);
            byte[] query = await udpTask;

            AssertEcsOption(query, "example.com");
        }
    }
}
