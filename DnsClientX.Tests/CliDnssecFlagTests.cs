using DnsClientX;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for CLI options that control DNSSEC flags on outbound queries.
    /// </summary>
    public class CliDnssecFlagTests {
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
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        private static void AssertDoCdBits(byte[] query, string name) {
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
            Assert.Equal(0x00008010u, ttl);
        }

        /// <summary>
        /// Running the CLI with <c>--dnssec</c> and <c>--validate-dnssec</c> should set the DO and CD bits.
        /// </summary>
        [Fact]
        public async Task Cli_ShouldSetDoAndCdBits_WhenDnssecValidationEnabled() {
            int port = TestUtilities.GetFreePort();

            SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });

            var response = CreateDnsHeader();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, response, cts.Token);

            try {
                var assembly = Assembly.Load("DnsClientX.Cli");
                Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
                MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());
                Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "--dnssec", "--validate-dnssec", "example.com" } })!;
                int exitCode = await task;
                Assert.Equal(0, exitCode);
            } finally {
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            byte[] query = await udpTask;
            AssertDoCdBits(query, "example.com");
        }
    }
}
