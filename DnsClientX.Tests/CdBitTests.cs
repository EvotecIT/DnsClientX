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
        private static async Task<byte[]> RunUdpServerAsync(UdpClient udp, CancellationToken token,
            Dictionary<(string Name, DnsRecordType Type), DnsResponse>? signedResponses = null) {
            using var stop = token.Register(udp.Dispose);
            byte[]? firstQuery = null;
            try {
                while (true) {
                    UdpReceiveResult result = await udp.ReceiveAsync();
                    firstQuery ??= result.Buffer;
                    byte[] response;
                    if (signedResponses == null) {
                        response = TestUtilities.CreateResponseFromQuery(result.Buffer);
                    } else {
                        var reader = new DnsWireReader(result.Buffer, 12);
                        string name = DnsWireNameCodec.Canonical(reader.ReadName());
                        var type = (DnsRecordType)reader.ReadUInt16();
                        reader.ReadUInt16();
                        response = DnsSecSignedFixture.SerializeResponse(result.Buffer, reader.Position,
                            signedResponses[(name, type)]);
                    }
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
                    if (signedResponses == null) return result.Buffer;
                }
            } catch (Exception ex) when (token.IsCancellationRequested &&
                ex is ObjectDisposedException or SocketException or OperationCanceledException) {
                return firstQuery ?? throw new OperationCanceledException(token);
            }
        }

        private static async Task<byte[]> RunTcpServerAsync(TcpListener listener, CancellationToken token) {
            using var stop = token.Register(listener.Stop);
            try {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                using var close = token.Register(client.Dispose);
                using NetworkStream stream = client.GetStream();
                byte[] lengthBuffer = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, lengthBuffer, 2, token);
                int length = (lengthBuffer[0] << 8) | lengthBuffer[1];
                byte[] queryBuffer = new byte[length];
                await TestUtilities.ReadExactlyAsync(stream, queryBuffer, length, token);
                byte[] response = TestUtilities.CreateResponseFromQuery(queryBuffer);
                byte[] prefix = { (byte)(response.Length >> 8), (byte)response.Length };
                await stream.WriteAsync(prefix, 0, prefix.Length, token);
                await stream.WriteAsync(response, 0, response.Length, token);
                return queryBuffer;
            } finally {
                listener.Stop();
            }
        }

        private static void AssertCdBit(byte[] query, string name, bool expectEdns, uint expectedOptTtl = 0) {
            Assert.Equal(0x10, query[3] & 0x10);
            int additionalCount = (query[10] << 8) | query[11];
            Assert.Equal(expectEdns ? 1 : 0, additionalCount);
            if (!expectEdns) return;

            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;

            Assert.Equal(0, query[offset]);
            ushort type = (ushort)((query[offset + 1] << 8) | query[offset + 2]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            uint ttl = (uint)((query[offset + 5] << 24) | (query[offset + 6] << 16) | (query[offset + 7] << 8) | query[offset + 8]);
            Assert.Equal(expectedOptTtl, ttl);
        }

        /// <summary>
        /// Ensures UDP queries set the CD bit when <see cref="Configuration.CheckingDisabled"/> is true.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldIncludeCdBit_WhenConfigured() {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, cts.Token);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) { Port = port, CheckingDisabled = true };
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

                AssertCdBit(query, "example.com", expectEdns: false);
            } finally {
                cts.Cancel();
                await udpTask;
            }
        }

        /// <summary>
        /// Ensures TCP queries set the CD bit when <see cref="Configuration.CheckingDisabled"/> is true.
        /// </summary>
        [Fact]
        public async Task TcpRequest_ShouldIncludeCdBit_WhenConfigured() {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var tcpTask = RunTcpServerAsync(listener, cts.Token);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverTCP) { Port = port, CheckingDisabled = true };
                DnsResponse response = await DnsWireResolveTcp.ResolveWireFormatTcp(
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
                Assert.Equal(DnsResponseCode.NoError, response.Status);
                Assert.Empty(response.Error);

                AssertCdBit(query, "example.com", expectEdns: false);
            } finally {
                cts.Cancel();
                await tcpTask;
            }
        }

        /// <summary>
        /// Explicit CD-bit configuration should not be cleared when validation is requested.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldKeepCdBit_WhenValidateDnsSecTrue_AndCheckingDisabledAlreadyTrue() {
            using var fixture = new DnsSecSignedFixture();
            var signedResponses = new Dictionary<(string Name, DnsRecordType Type), DnsResponse> {
                [("example.com.", DnsRecordType.A)] = fixture.Signed("example.com", DnsRecordType.A, new byte[] { 192, 0, 2, 1 }),
                [(".", DnsRecordType.DNSKEY)] = await fixture.LookupAsync(".", DnsRecordType.DNSKEY, CancellationToken.None),
                [(fixture.Zone.Name, DnsRecordType.DNSKEY)] = await fixture.LookupAsync(fixture.Zone.Name, DnsRecordType.DNSKEY, CancellationToken.None),
                [(fixture.Zone.Name, DnsRecordType.DS)] = await fixture.LookupAsync(fixture.Zone.Name, DnsRecordType.DS, CancellationToken.None)
            };
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, cts.Token, signedResponses);

            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP, useTcpFallback: false);
            client.EndpointConfiguration.Port = port;
            client.EndpointConfiguration.Rfc5011TrustAnchorStorePath = fixture.AnchorPath;
            client.EndpointConfiguration.CheckingDisabled = true;

            DnsResponse response;
            try {
                response = await client.Resolve("example.com", DnsRecordType.A, requestDnsSec: false, validateDnsSec: true, retryOnTransient: false, cancellationToken: cts.Token);
            } finally {
                cts.Cancel();
                await udpTask;
            }

            byte[] query = await udpTask;
            Assert.Equal(DnsResponseCode.NoError, response.Status);
            Assert.Empty(response.Error);
            Assert.Equal(DnsSecValidationStatus.Secure, response.DnsSecValidationStatus);

            AssertCdBit(query, "example.com", expectEdns: true, expectedOptTtl: 0x0000C000u);
            Assert.True(client.EndpointConfiguration.CheckingDisabled);
        }

        /// <summary>
        /// Validation should restore the original CD-bit setting even when resolution throws.
        /// </summary>
        [Fact]
        public async Task Resolve_ShouldRestoreCheckingDisabled_AfterFailure() {
            using var client = new ClientX("1.1.1.1", DnsRequestFormat.DnsOverHttps);
            client.EndpointConfiguration.CheckingDisabled = false;
            client.EndpointConfiguration.RequestFormat = (DnsRequestFormat)999;

            await Assert.ThrowsAsync<DnsClientException>(
                () => client.Resolve("example.com", DnsRecordType.A, requestDnsSec: false, validateDnsSec: true, retryOnTransient: false));

            Assert.False(client.EndpointConfiguration.CheckingDisabled);
        }

        /// <summary>
        /// Ensures serialized messages include the CD bit without setting DO.
        /// </summary>
        [Fact]
        public void Message_ShouldIncludeCdBit_WhenConfigured() {
            var message = new DnsMessage("example.com", DnsRecordType.A, false, true, 4096, null, true);
            byte[] data = message.SerializeDnsWireFormat();
            AssertCdBit(data, "example.com", expectEdns: true);
        }
    }
}
