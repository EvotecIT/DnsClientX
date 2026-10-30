using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests EDNS options handling for wire format queries.
    /// </summary>
    public class EdnsOptionsTests {
        private static async Task<byte[]> RunUdpServerAsync(UdpClient udp, CancellationToken token) {
            using var stop = token.Register(udp.Dispose);
            UdpReceiveResult result = await udp.ReceiveAsync();
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        private static void AssertEcsOption(byte[] query, string name) {
            Dictionary<ushort, byte[]> options = ReadOptOptions(query, name);
            Assert.True(options.ContainsKey(8));
        }

        private static Dictionary<ushort, byte[]> ReadOptOptions(byte[] query, string name) {
            int offset = 12;
            foreach (var label in name.Split('.')) {
                offset += 1 + label.Length;
            }
            offset += 1 + 2 + 2;

            Assert.Equal(0, query[offset]);
            offset += 1;
            ushort type = (ushort)((query[offset] << 8) | query[offset + 1]);
            Assert.Equal((ushort)DnsRecordType.OPT, type);
            offset += 2 + 2 + 4;
            ushort rdlen = (ushort)((query[offset] << 8) | query[offset + 1]);
            Assert.True(rdlen > 0);
            offset += 2;

            int end = offset + rdlen;
            var options = new Dictionary<ushort, byte[]>();
            while (offset < end) {
                ushort optionCode = (ushort)((query[offset] << 8) | query[offset + 1]);
                ushort optionLength = (ushort)((query[offset + 2] << 8) | query[offset + 3]);
                offset += 4;

                byte[] optionData = new byte[optionLength];
                Array.Copy(query, offset, optionData, 0, optionLength);
                options[optionCode] = optionData;
                offset += optionLength;
            }

            return options;
        }

        /// <summary>
        /// Ensures the ECS option is added when EDNS is enabled.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldIncludeEcsOption_WhenOptionsConfigured() {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, cts.Token);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                    Port = port,
                    EdnsOptions = new EdnsOptions { EnableEdns = true, Subnet = new EdnsClientSubnetOption("192.0.2.1/24") }
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

        /// <summary>
        /// Ensures padding and cookie options are serialized when configured.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldIncludePaddingAndCookie_WhenOptionsConfigured() {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, cts.Token);
            try {
                byte[] cookie = { 1, 2, 3, 4, 5, 6, 7, 8 };

                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                    Port = port,
                    EdnsOptions = new EdnsOptions {
                        EnableEdns = true,
                        PaddingLength = 12,
                        Cookie = cookie
                    }
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
                Dictionary<ushort, byte[]> options = ReadOptOptions(query, "example.com");

                Assert.True(options.ContainsKey(10));
                Assert.True(options.ContainsKey(12));
                Assert.Equal(cookie, options[10]);
                Assert.Equal(12, options[12].Length);
                Assert.All(options[12], value => Assert.Equal(0, value));
            } finally {
                cts.Cancel();
                await udpTask;
            }
        }

        /// <summary>
        /// Verifies that null EDNS options do not throw during UDP requests.
        /// </summary>
        [Fact]
        public async Task UdpRequest_ShouldNotThrow_WhenEdnsOptionsNull() {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(udp, cts.Token);
            try {
                var config = new Configuration("127.0.0.1", DnsRequestFormat.DnsOverUDP) {
                    Port = port,
                    EdnsOptions = null
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
                Assert.Equal(0, (query[10] << 8) | query[11]);
            } finally {
                cts.Cancel();
                await udpTask;
            }
        }
    }
}
