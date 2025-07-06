using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
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
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static async Task<byte[]> RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            UdpReceiveResult result = await udp.ReceiveAsync();
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
            Type type = typeof(ClientX).Assembly.GetType("DnsClientX.DnsWireResolveUdp")!;
            MethodInfo method = type.GetMethod("ResolveWireFormatUdp", BindingFlags.Static | BindingFlags.NonPublic)!;
            var task = (Task<DnsResponse>)method.Invoke(null, new object[] { "127.0.0.1", port, "example.com", DnsRecordType.A, false, false, false, config, cts.Token })!;
            await task;
            byte[] query = await udpTask;

            AssertEcsOption(query, "example.com");
        }
    }
}
