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
    /// Tests CLI output modes for standard query execution.
    /// </summary>
    [Collection("NoParallel")]
    public class CliQueryOutputTests {
        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        private static async Task RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token);
            await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
            if (completed != receiveTask) {
                throw new OperationCanceledException(token);
            }
            UdpReceiveResult result = receiveTask.Result;
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
        }

        private static byte[] BuildQueryOutputResponse() {
            using var ms = new MemoryStream();

            WriteUInt16(ms, 0x1234);
            WriteUInt16(ms, 0x8180);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 1);

            WriteName(ms, "example.com");
            WriteUInt16(ms, (ushort)DnsRecordType.A);
            WriteUInt16(ms, 1);

            WriteARecord(ms, "example.com", 60, "203.0.113.10");
            WriteARecord(ms, "ns1.example.com", 300, "203.0.113.53");
            WriteARecord(ms, "extra.example.com", 300, "203.0.113.99");

            return ms.ToArray();
        }

        private static byte[] BuildTxtResponse() {
            using var ms = new MemoryStream();

            WriteUInt16(ms, 0x1234);
            WriteUInt16(ms, 0x8180);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);

            WriteName(ms, "example.com");
            WriteUInt16(ms, (ushort)DnsRecordType.TXT);
            WriteUInt16(ms, 1);

            WriteTxtRecord(ms, "example.com", 60, "line1\nline2");

            return ms.ToArray();
        }

        private static async Task RunPtrEchoServerAsync(int port, Action<string, DnsRecordType> onQuery, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
#if NET5_0_OR_GREATER
            UdpReceiveResult result = await udp.ReceiveAsync(token);
#else
            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
            if (completed != receiveTask) {
                throw new OperationCanceledException(token);
            }
            UdpReceiveResult result = receiveTask.Result;
#endif

            (ushort id, string questionName, DnsRecordType questionType) = ParseQuestion(result.Buffer);
            onQuery(questionName, questionType);
            byte[] response = BuildPtrResponse(id, questionName, "one.one.one.one");
#if NET5_0_OR_GREATER
            await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
        }

        private static (ushort Id, string QuestionName, DnsRecordType QuestionType) ParseQuestion(byte[] message) {
            ushort id = (ushort)((message[0] << 8) | message[1]);
            int offset = 12;
            string questionName = ReadName(message, ref offset);
            ushort qtype = (ushort)((message[offset] << 8) | message[offset + 1]);
            return (id, questionName, (DnsRecordType)qtype);
        }

        private static byte[] BuildPtrResponse(ushort id, string questionName, string ptrTarget) {
            using var ms = new MemoryStream();

            WriteUInt16(ms, id);
            WriteUInt16(ms, 0x8180);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);

            WriteName(ms, questionName);
            WriteUInt16(ms, (ushort)DnsRecordType.PTR);
            WriteUInt16(ms, 1);

            WritePtrRecord(ms, questionName, 60, ptrTarget);

            return ms.ToArray();
        }

        private static void WriteARecord(Stream stream, string name, int ttl, string ipAddress) {
            WriteName(stream, name);
            WriteUInt16(stream, (ushort)DnsRecordType.A);
            WriteUInt16(stream, 1);
            WriteUInt32(stream, (uint)ttl);
            WriteUInt16(stream, 4);

            byte[] octets = IPAddress.Parse(ipAddress).GetAddressBytes();
            stream.Write(octets, 0, octets.Length);
        }

        private static void WritePtrRecord(Stream stream, string name, int ttl, string target) {
            WriteName(stream, name);
            WriteUInt16(stream, (ushort)DnsRecordType.PTR);
            WriteUInt16(stream, 1);
            WriteUInt32(stream, (uint)ttl);

            using var rdata = new MemoryStream();
            WriteName(rdata, target);
            WriteUInt16(stream, (ushort)rdata.Length);
            rdata.Position = 0;
            rdata.CopyTo(stream);
        }

        private static void WriteTxtRecord(Stream stream, string name, int ttl, params string[] values) {
            WriteName(stream, name);
            WriteUInt16(stream, (ushort)DnsRecordType.TXT);
            WriteUInt16(stream, 1);
            WriteUInt32(stream, (uint)ttl);

            using var rdata = new MemoryStream();
            foreach (string value in values) {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                rdata.WriteByte((byte)bytes.Length);
                rdata.Write(bytes, 0, bytes.Length);
            }

            WriteUInt16(stream, (ushort)rdata.Length);
            rdata.Position = 0;
            rdata.CopyTo(stream);
        }

        private static void WriteName(Stream stream, string name) {
            foreach (string label in name.Split('.')) {
                stream.WriteByte((byte)label.Length);
                foreach (char c in label) {
                    stream.WriteByte((byte)c);
                }
            }
            stream.WriteByte(0);
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

        /// <summary>
        /// Ensures JSON query output emits a structured response payload.
        /// </summary>
        [Fact]
        public async Task JsonFormat_PrintsStructuredResponse() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, BuildQueryOutputResponse(), cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--format", "json", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("\"Status\": \"NoError\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Answer\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Authority\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Additional\"", text, StringComparison.Ordinal);
                Assert.Contains("\"data\": \"203.0.113.10\"", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures short query output prints answer values only.
        /// </summary>
        [Fact]
        public async Task ShortOutput_PrintsAnswerValuesOnly() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, BuildQueryOutputResponse(), cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--short", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString().Trim();
                Assert.Equal("203.0.113.10", text);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures raw output respects explicit section selection flags.
        /// </summary>
        [Fact]
        public async Task RawFormat_RespectsSectionSelection() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, BuildQueryOutputResponse(), cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--format", "raw", "--question", "--authority", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains(";; QUESTION SECTION:", text, StringComparison.Ordinal);
                Assert.Contains(";; AUTHORITY SECTION:", text, StringComparison.Ordinal);
                Assert.DoesNotContain(";; ANSWER SECTION:", text, StringComparison.Ordinal);
                Assert.DoesNotContain(";; ADDITIONAL SECTION:", text, StringComparison.Ordinal);
                Assert.Contains(";example.com", text, StringComparison.Ordinal);
                Assert.Contains("ns1.example.com", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures IP input automatically triggers PTR lookup behavior.
        /// </summary>
        [Fact]
        public async Task IpInput_DefaultsToPtrLookup() {
            int port = TestUtilities.GetFreeUdpPort();
            string? receivedQuestionName = null;
            DnsRecordType receivedQuestionType = DnsRecordType.A;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunPtrEchoServerAsync(port, (name, type) => {
                receivedQuestionName = name;
                receivedQuestionType = type;
            }, cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--format", "raw", "--question", "--answer", "1.1.1.1");

                Assert.Equal(0, exitCode);
                Assert.Equal("1.1.1.1.in-addr.arpa", receivedQuestionName);
                Assert.Equal(DnsRecordType.PTR, receivedQuestionType);

                string text = output.ToString();
                Assert.Contains(";1.1.1.1.in-addr.arpa", text, StringComparison.Ordinal);
                Assert.Contains("\tIN\tPTR\tone.one.one.one", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures TXT concatenation output flattens line-oriented TXT values.
        /// </summary>
        [Fact]
        public async Task TxtConcatOption_FlattensTxtOutput() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, BuildTxtResponse(), cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--short", "--txt-concat", "--type", "TXT", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString().Trim();
                Assert.Equal("line1line2", text);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }
    }
}
