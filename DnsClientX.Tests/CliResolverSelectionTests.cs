using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests CLI resolver selection from saved score snapshots.
    /// </summary>
    [Collection("NoParallel")]
    public class CliResolverSelectionTests {
        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        private static void SetProbeOverride(Func<DnsEndpoint, string, CancellationToken, Task<(DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat)>>? probeOverride) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            FieldInfo field = programType.GetField("ProbeOverride", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, probeOverride);
        }

        private static void SetProbeEndpointOverride(Func<DnsResolverEndpoint, string, CancellationToken, Task<(DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat)>>? probeOverride) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            FieldInfo field = programType.GetField("ProbeEndpointOverride", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, probeOverride);
        }

        private static string WriteSnapshotFile(ResolverScoreSnapshot snapshot) {
            string path = Path.GetTempFileName();
            var serializerOptions = new JsonSerializerOptions {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            serializerOptions.Converters.Add(new JsonStringEnumConverter());
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot, serializerOptions));
            return path;
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

        private static void WriteName(Stream stream, string name) {
            foreach (string label in name.Split('.')) {
                stream.WriteByte((byte)label.Length);
                foreach (char c in label) {
                    stream.WriteByte((byte)c);
                }
            }

            stream.WriteByte(0);
        }

        private static byte[] BuildAResponse(ushort id, string questionName, string ipAddress) {
            using var ms = new MemoryStream();
            WriteUInt16(ms, id);
            WriteUInt16(ms, 0x8180);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 1);
            WriteUInt16(ms, 0);
            WriteUInt16(ms, 0);

            WriteName(ms, questionName);
            WriteUInt16(ms, (ushort)DnsRecordType.A);
            WriteUInt16(ms, 1);

            WriteName(ms, questionName);
            WriteUInt16(ms, (ushort)DnsRecordType.A);
            WriteUInt16(ms, 1);
            WriteUInt32(ms, 60);
            WriteUInt16(ms, 4);
            byte[] addressBytes = IPAddress.Parse(ipAddress).GetAddressBytes();
            ms.Write(addressBytes, 0, addressBytes.Length);

            return ms.ToArray();
        }

        private static (ushort Id, string QuestionName, DnsRecordType QuestionType) ParseQuestion(byte[] message) {
            ushort id = (ushort)((message[0] << 8) | message[1]);
            int offset = 12;
            string questionName = ReadName(message, ref offset);
            ushort qtype = (ushort)((message[offset] << 8) | message[offset + 1]);
            return (id, questionName, (DnsRecordType)qtype);
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

        private static async Task RunUdpServerAsync(int port, Func<byte[], byte[]> buildResponse, CancellationToken token) {
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

        /// <summary>
        /// Ensures resolver selection prints the recommended explicit endpoint by default.
        /// </summary>
        [Fact]
        public async Task ResolverSelect_PrintsRecommendedExplicitEndpoint() {
            string snapshotPath = WriteSnapshotFile(new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Probe,
                    RecommendationAvailable = true,
                    RecommendedTarget = "udp@1.1.1.1:53",
                    RecommendedResolver = "1.1.1.1:53",
                    RecommendedTransport = "Udp",
                    RecommendedAverageMs = 5,
                    RecommendationSource = "consensus_leader",
                    RecommendationStatus = "recommended",
                    RecommendationReason = "none"
                }
            });

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-select", snapshotPath);

                Assert.Equal(0, exitCode);
                Assert.Equal("udp@1.1.1.1:53", output.ToString().Trim());
            } finally {
                Console.SetOut(originalOut);
                File.Delete(snapshotPath);
            }
        }

        /// <summary>
        /// Ensures saved selections can drive standard query mode with an explicit endpoint.
        /// </summary>
        [Fact]
        public async Task ResolverUse_QueryMode_UsesExplicitEndpointSelection() {
            int port = TestUtilities.GetFreeUdpPort();
            string snapshotPath = WriteSnapshotFile(new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Probe,
                    RecommendationAvailable = true,
                    RecommendedTarget = $"udp@127.0.0.1:{port}",
                    RecommendedResolver = $"127.0.0.1:{port}",
                    RecommendedTransport = "Udp",
                    RecommendedAverageMs = 4,
                    RecommendationSource = "consensus_leader",
                    RecommendationStatus = "recommended",
                    RecommendationReason = "none"
                }
            });

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, buffer => {
                (ushort id, string questionName, DnsRecordType questionType) = ParseQuestion(buffer);
                Assert.Equal("example.com", questionName);
                Assert.Equal(DnsRecordType.A, questionType);
                return BuildAResponse(id, questionName, "203.0.113.10");
            }, cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-use", snapshotPath, "--short", "example.com");

                Assert.Equal(0, exitCode);
                Assert.Equal("203.0.113.10", output.ToString().Trim());
            } finally {
                Console.SetOut(originalOut);
                File.Delete(snapshotPath);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures saved selections can drive probe mode with an explicit endpoint.
        /// </summary>
        [Fact]
        public async Task ResolverUse_ProbeMode_UsesExplicitEndpointSelection() {
            string snapshotPath = WriteSnapshotFile(new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Probe,
                    RecommendationAvailable = true,
                    RecommendedTarget = "udp@1.1.1.1:53",
                    RecommendedResolver = "1.1.1.1:53",
                    RecommendedTransport = "Udp",
                    RecommendedAverageMs = 5,
                    RecommendationSource = "consensus_leader",
                    RecommendationStatus = "recommended",
                    RecommendationReason = "none"
                }
            });

            SetProbeEndpointOverride((endpoint, domain, token) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport
                },
                TimeSpan.FromMilliseconds(5),
                $"{endpoint.Host}:{endpoint.Port}",
                DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-use", snapshotPath, "--probe", "--probe-summary-only", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Endpoint profile: selected", text, StringComparison.Ordinal);
                Assert.Contains("Candidates: 1", text, StringComparison.Ordinal);
                Assert.Contains("Recommended endpoint: udp@1.1.1.1:53 in 5 ms via Udp", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
                File.Delete(snapshotPath);
            }
        }

        /// <summary>
        /// Ensures saved selections can drive benchmark mode with a built-in endpoint.
        /// </summary>
        [Fact]
        public async Task ResolverUse_BenchmarkMode_UsesBuiltInSelection() {
            string snapshotPath = WriteSnapshotFile(new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Benchmark,
                    RecommendationAvailable = true,
                    RecommendedTarget = "Cloudflare",
                    RecommendedResolver = "1.1.1.1:53",
                    RecommendedTransport = "Doh",
                    RecommendedAverageMs = 7,
                    RecommendationSource = "policy_pass",
                    RecommendationStatus = "recommended",
                    RecommendationReason = "none"
                }
            });

            SetProbeOverride((endpoint, domain, token) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = Transport.Doh
                },
                TimeSpan.FromMilliseconds(7),
                "1.1.1.1:53",
                DnsRequestFormat.DnsOverHttps
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync(
                    "--resolver-use", snapshotPath,
                    "--benchmark",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "1",
                    "--benchmark-summary-only");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Endpoint profile: selected", text, StringComparison.Ordinal);
                Assert.Contains("Candidates: 1", text, StringComparison.Ordinal);
                Assert.Contains("Best endpoint: Cloudflare in 7 ms average (100% success)", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
                File.Delete(snapshotPath);
            }
        }

        /// <summary>
        /// Ensures resolver selection can emit structured JSON for built-in resolver profiles.
        /// </summary>
        [Fact]
        public async Task ResolverSelect_JsonFormat_PrintsBuiltInSelection() {
            string snapshotPath = WriteSnapshotFile(new ResolverScoreSnapshot {
                Summary = new ResolverScoreSummary {
                    Mode = ResolverScoreMode.Benchmark,
                    RecommendationAvailable = true,
                    RecommendedTarget = "Cloudflare",
                    RecommendedResolver = "1.1.1.1:53",
                    RecommendedTransport = "Doh",
                    RecommendedAverageMs = 7,
                    RecommendationSource = "policy_pass",
                    RecommendationStatus = "recommended",
                    RecommendationReason = "none"
                }
            });

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-select", snapshotPath, "--format", "json");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("\"Kind\": \"BuiltInEndpoint\"", text, StringComparison.Ordinal);
                Assert.Contains("\"BuiltInEndpoint\": \"Cloudflare\"", text, StringComparison.Ordinal);
                Assert.Contains("\"Target\": \"Cloudflare\"", text, StringComparison.Ordinal);
            } finally {
                Console.SetOut(originalOut);
                File.Delete(snapshotPath);
            }
        }
    }
}
