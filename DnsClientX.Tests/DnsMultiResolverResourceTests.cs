using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Resource lifetime and wire-flag tests for <see cref="DnsMultiResolver"/>.
    /// </summary>
    [Collection("DisposalTests")]
    public class DnsMultiResolverResourceTests {
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

        private static async Task<byte[]> RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            UdpReceiveResult result = await udp.ReceiveAsync();
            token.ThrowIfCancellationRequested();
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        private static int GetAdditionalCount(byte[] query) {
            return (query[10] << 8) | query[11];
        }

        private static void AssertDoBitWithoutCd(byte[] query, string name) {
            Assert.Equal(1, GetAdditionalCount(query));

            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;

            Assert.Equal(0, query[offset]);
            ushort type = (ushort)((query[offset + 1] << 8) | query[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            uint ttl = (uint)((query[offset + 5] << 24) | (query[offset + 6] << 16) | (query[offset + 7] << 8) | query[offset + 8]);
            Assert.Equal(0x00008000u, ttl);
        }

        /// <summary>
        /// Each real query should dispose the temporary <see cref="ClientX"/> instance created per endpoint.
        /// </summary>
        [Fact]
        public async Task QueryAsync_ShouldDisposeTemporaryClient() {
            int initialCount = ClientX.DisposalCount;
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, CreateDnsHeader(), cts.Token);

            var endpoint = new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp };
            using var resolver = new DnsMultiResolver(new[] { endpoint });

            DnsResponse response = await resolver.QueryAsync("example.com", DnsRecordType.A, cts.Token);

            await udpTask;
            await Task.Delay(50);

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.True(ClientX.DisposalCount - initialCount >= 1, $"Initial={initialCount} Final={ClientX.DisposalCount}");
        }

        /// <summary>
        /// A false <see cref="DnsResolverEndpoint.DnsSecOk"/> value should not emit EDNS or CD bits.
        /// </summary>
        [Fact]
        public async Task QueryAsync_DnsSecOkFalse_ShouldNotSetEdnsFlags() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, CreateDnsHeader(), cts.Token);

            var endpoint = new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp, DnsSecOk = false };
            using var resolver = new DnsMultiResolver(new[] { endpoint });

            DnsResponse response = await resolver.QueryAsync("example.com", DnsRecordType.A, cts.Token);
            byte[] query = await udpTask;

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.Equal(0, GetAdditionalCount(query));
        }

        /// <summary>
        /// A true <see cref="DnsResolverEndpoint.DnsSecOk"/> value should request DNSSEC without setting CD.
        /// </summary>
        [Fact]
        public async Task QueryAsync_DnsSecOkTrue_ShouldSetDoBitOnly() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, CreateDnsHeader(), cts.Token);

            var endpoint = new DnsResolverEndpoint { Host = "127.0.0.1", Port = port, Transport = Transport.Udp, DnsSecOk = true };
            using var resolver = new DnsMultiResolver(new[] { endpoint });

            DnsResponse response = await resolver.QueryAsync("example.com", DnsRecordType.A, cts.Token);
            byte[] query = await udpTask;

            Assert.Equal(DnsResponseCode.NoError, response.Status);
            AssertDoBitWithoutCd(query, "example.com");
        }
    }
}
