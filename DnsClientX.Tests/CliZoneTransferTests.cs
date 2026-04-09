using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests CLI recursive zone transfer behavior.
    /// </summary>
    [Collection("NoParallel")]
    public class CliZoneTransferTests {
        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        private static byte[] EncodeName(string name) {
            name = name.TrimEnd('.');
            using var ms = new MemoryStream();
            foreach (string label in name.Split('.')) {
                byte[] bytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)bytes.Length);
                ms.Write(bytes, 0, bytes.Length);
            }

            ms.WriteByte(0);
            return ms.ToArray();
        }

        private static void WriteUInt16(Stream stream, ushort value) {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteUInt32(Stream stream, uint value) {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static string ReadName(byte[] message, ref int offset) {
            var builder = new StringBuilder();
            while (offset < message.Length) {
                int length = message[offset++];
                if (length == 0) {
                    break;
                }

                if (builder.Length > 0) {
                    builder.Append('.');
                }

                builder.Append(Encoding.ASCII.GetString(message, offset, length));
                offset += length;
            }

            return builder.ToString();
        }

        private static (ushort Id, string QuestionName, DnsRecordType QuestionType) ParseQuestion(byte[] message) {
            ushort id = (ushort)((message[0] << 8) | message[1]);
            int offset = 12;
            string questionName = ReadName(message, ref offset);
            ushort qtype = (ushort)((message[offset] << 8) | message[offset + 1]);
            return (id, questionName, (DnsRecordType)qtype);
        }

        private static byte[] BuildNsResponse(ushort id, string zone, params (string Authority, string GlueAddress)[] authorities) {
            using var ms = new MemoryStream();
            WriteUInt16(ms, id);
            WriteUInt16(ms, 0x8180);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, (ushort)authorities.Length);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, (ushort)authorities.Length);

            byte[] zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.NS);
            WriteUInt16(ms, 1);

            foreach ((string authority, _) in authorities) {
                ms.Write(zoneBytes, 0, zoneBytes.Length);
                WriteUInt16(ms, (ushort)DnsRecordType.NS);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 60);

                byte[] authorityBytes = EncodeName(authority);
                WriteUInt16(ms, (ushort)authorityBytes.Length);
                ms.Write(authorityBytes, 0, authorityBytes.Length);
            }

            foreach ((string authority, string glueAddress) in authorities) {
                byte[] authorityBytes = EncodeName(authority);
                ms.Write(authorityBytes, 0, authorityBytes.Length);
                WriteUInt16(ms, (ushort)DnsRecordType.A);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 60);

                byte[] addressBytes = IPAddress.Parse(glueAddress).GetAddressBytes();
                WriteUInt16(ms, (ushort)addressBytes.Length);
                ms.Write(addressBytes, 0, addressBytes.Length);
            }

            return ms.ToArray();
        }

        private static byte[] BuildSoaRdata() {
            using var ms = new MemoryStream();
            byte[] nsBytes = EncodeName("ns1.example.com");
            ms.Write(nsBytes, 0, nsBytes.Length);
            byte[] hostBytes = EncodeName("hostmaster.example.com");
            ms.Write(hostBytes, 0, hostBytes.Length);
            WriteUInt32(ms, 1);
            WriteUInt32(ms, 3600);
            WriteUInt32(ms, 600);
            WriteUInt32(ms, 86400);
            WriteUInt32(ms, 60);
            return ms.ToArray();
        }

        private static byte[] BuildAxfrMessage(string zone, params (string Name, DnsRecordType Type, byte[] Data)[] answers) {
            using var ms = new MemoryStream();
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0x8400);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, (ushort)answers.Length);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);

            byte[] zoneBytes = EncodeName(zone);
            ms.Write(zoneBytes, 0, zoneBytes.Length);
            WriteUInt16(ms, (ushort)DnsRecordType.AXFR);
            WriteUInt16(ms, 1);

            foreach ((string name, DnsRecordType type, byte[] data) in answers) {
                byte[] nameBytes = EncodeName(name);
                ms.Write(nameBytes, 0, nameBytes.Length);
                WriteUInt16(ms, (ushort)type);
                WriteUInt16(ms, 1);
                WriteUInt32(ms, 3600);
                WriteUInt16(ms, (ushort)data.Length);
                ms.Write(data, 0, data.Length);
            }

            return ms.ToArray();
        }

        private static byte[] BuildARecordData(string ipAddress) {
            return IPAddress.Parse(ipAddress).GetAddressBytes();
        }

        private static async Task RunDiscoveryServerAsync(int port, Func<byte[], byte[]> buildResponse, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token);
            byte[] response = buildResponse(result.Buffer);
            await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
            if (completed != receiveTask) {
                throw new OperationCanceledException(token);
            }

            UdpReceiveResult result = receiveTask.Result;
            byte[] response = buildResponse(result.Buffer);
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
        }

        private static async Task RunAxfrServerAsync(int port, byte[][] responses, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            try {
#if NETFRAMEWORK
                using TcpClient client = await listener.AcceptTcpClientAsync();
#else
                using TcpClient client = await listener.AcceptTcpClientAsync(token);
#endif
                using NetworkStream stream = client.GetStream();
                byte[] lengthBuffer = new byte[2];
                await TestUtilities.ReadExactlyAsync(stream, lengthBuffer, 2, token);
                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(lengthBuffer);
                }

                int queryLength = BitConverter.ToUInt16(lengthBuffer, 0);
                byte[] queryBuffer = new byte[queryLength];
                await TestUtilities.ReadExactlyAsync(stream, queryBuffer, queryLength, token);

                foreach (byte[] response in responses) {
                    byte[] prefix = BitConverter.GetBytes((ushort)response.Length);
                    if (BitConverter.IsLittleEndian) {
                        Array.Reverse(prefix);
                    }

#if NET5_0_OR_GREATER
                    await stream.WriteAsync(prefix, token);
                    await stream.WriteAsync(response, token);
#else
                    await stream.WriteAsync(prefix, 0, prefix.Length, token);
                    await stream.WriteAsync(response, 0, response.Length, token);
#endif
                }
            } finally {
                listener.Stop();
            }
        }

        /// <summary>
        /// Ensures CLI AXFR mode prints the transfer summary and returned records.
        /// </summary>
        [Fact]
        public async Task AxfrOption_PrintsTransferSummaryAndRecords() {
            int port = TestUtilities.GetFreeTcpPort();
            byte[] soa = BuildSoaRdata();
            byte[] opening = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] record = BuildAxfrMessage("example.com", ("www.example.com", DnsRecordType.A, BuildARecordData("203.0.113.10")));
            byte[] closing = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task discoveryTask = RunDiscoveryServerAsync(port, buffer => {
                (ushort id, string questionName, DnsRecordType questionType) = ParseQuestion(buffer);
                Assert.Equal("example.com", questionName);
                Assert.Equal(DnsRecordType.NS, questionType);
                return BuildNsResponse(id, questionName, ("ns1.example.com", "127.0.0.1"));
            }, cts.Token);
            Task axfrTask = RunAxfrServerAsync(port, new[] { opening, record, closing }, cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--axfr", "--transfer-summary", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Zone Transfer: example.com", text, StringComparison.Ordinal);
                Assert.Contains("Selected authority: ns1.example.com", text, StringComparison.Ordinal);
                Assert.Contains($"Selected server: 127.0.0.1:{port}", text, StringComparison.Ordinal);
                Assert.Contains("Authorities discovered: ns1.example.com", text, StringComparison.Ordinal);
                Assert.Contains("Tried servers: 127.0.0.1", text, StringComparison.Ordinal);
                Assert.Contains("www.example.com", text, StringComparison.Ordinal);
                Assert.Contains("203.0.113.10", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await Task.WhenAll(discoveryTask, axfrTask);
        }

        /// <summary>
        /// Ensures CLI AXFR mode supports JSON output for structured automation use.
        /// </summary>
        [Fact]
        public async Task AxfrOption_JsonFormat_PrintsStructuredTransferResult() {
            int port = TestUtilities.GetFreeTcpPort();
            byte[] soa = BuildSoaRdata();
            byte[] opening = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));
            byte[] record = BuildAxfrMessage("example.com", ("www.example.com", DnsRecordType.A, BuildARecordData("203.0.113.20")));
            byte[] closing = BuildAxfrMessage("example.com", ("example.com", DnsRecordType.SOA, soa));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task discoveryTask = RunDiscoveryServerAsync(port, buffer => {
                (ushort id, string questionName, DnsRecordType questionType) = ParseQuestion(buffer);
                Assert.Equal("example.com", questionName);
                Assert.Equal(DnsRecordType.NS, questionType);
                return BuildNsResponse(id, questionName, ("ns1.example.com", "127.0.0.1"));
            }, cts.Token);
            Task axfrTask = RunAxfrServerAsync(port, new[] { opening, record, closing }, cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--axfr", "--format", "json", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("\"Zone\": \"example.com\"", text, StringComparison.Ordinal);
                Assert.Contains("\"SelectedAuthority\": \"ns1.example.com\"", text, StringComparison.Ordinal);
                Assert.Contains("\"SelectedServer\": \"127.0.0.1\"", text, StringComparison.Ordinal);
                Assert.Contains("\"RecordSets\"", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await Task.WhenAll(discoveryTask, axfrTask);
        }
    }
}
