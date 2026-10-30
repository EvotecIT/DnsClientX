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
        private static async Task<byte[]> RunUdpServerAsync(UdpClient udp, CancellationToken token) {
            using var stop = token.Register(udp.Dispose);
            UdpReceiveResult result = await udp.ReceiveAsync();
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
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
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, cts.Token);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                    Port = port,
                    EnableEdns = true,
                    Subnet = "192.0.2.1/24"
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
                    1,
                    cts.Token);
                byte[] query = await udpTask;
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Empty(response.Error);

                AssertEcsOption(query, "example.com");
            } finally {
                cts.Cancel();
                await udpTask;
            }
        }
    }
}
