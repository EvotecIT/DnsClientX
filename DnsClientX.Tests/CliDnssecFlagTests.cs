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
    [Collection("NoParallel")]
    public class CliDnssecFlagTests {

        private static async Task<byte[]> RunUdpServerAsync(int port, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            using var cancellation = token.Register(udp.Dispose);
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token);
#else
            UdpReceiveResult result = await udp.ReceiveAsync();
#endif
            byte[] response = TestUtilities.CreateResponseFromQuery(result.Buffer);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
            return result.Buffer;
        }

        private static void AssertDoCdBits(byte[] query, string name) {
            Assert.Equal(0x10, query[3] & 0x10);
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
            Assert.Equal(0x0000C000u, ttl);
        }

        /// <summary>
        /// Running the CLI with <c>--dnssec</c> and <c>--validate-dnssec</c> should set the DO and CD bits.
        /// </summary>
        [Fact]
        public async Task Cli_ShouldSetDoAndCdBits_WhenDnssecValidationEnabled() {
            int port = TestUtilities.GetFreeUdpPort();

            SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var udpTask = RunUdpServerAsync(port, cts.Token);

            try {
                var assembly = Assembly.Load("DnsClientX.Cli");
                Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
                MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());
                Task<int> task = (Task<int>)main.Invoke(null, new object[] { new[] { "--dnssec", "--validate-dnssec", "example.com" } })!;
                int exitCode = await task;
                Assert.Equal(1, exitCode);
            } finally {
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            byte[] query = await udpTask;
            AssertDoCdBits(query, "example.com");
        }
    }
}
