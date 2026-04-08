using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests CLI resolver import workflows for probe and benchmark modes.
    /// </summary>
    [Collection("NoParallel")]
    public class CliResolverImportTests {
        private static async Task<int> InvokeCliAsync(params string[] args) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo main = programType.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static)!;
            Task<int> task = (Task<int>)main.Invoke(null, new object[] { args })!;
            return await task;
        }

        private static void SetProbeEndpointOverride(Func<DnsResolverEndpoint, string, CancellationToken, Task<(DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat)>>? probeOverride) {
            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            FieldInfo field = programType.GetField("ProbeEndpointOverride", BindingFlags.NonPublic | BindingFlags.Static)!;
            field.SetValue(null, probeOverride);
        }

        private static async Task RunHttpServerAsync(int port, string body, CancellationToken token) {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            try {
                TcpClient client;
#if NET8_0_OR_GREATER
                client = await listener.AcceptTcpClientAsync(token);
#else
                var acceptTask = listener.AcceptTcpClientAsync();
                var completed = await Task.WhenAny(acceptTask, Task.Delay(Timeout.Infinite, token));
                if (completed != acceptTask) {
                    throw new OperationCanceledException(token);
                }
                client = acceptTask.Result;
#endif

                using (client)
                using (NetworkStream stream = client.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true)) {
                    while (true) {
                        string? line = await reader.ReadLineAsync();
                        if (line is null || line.Length == 0) {
                            break;
                        }
                    }

                    byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                    string headers =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/plain; charset=utf-8\r\n" +
                        $"Content-Length: {bodyBytes.Length}\r\n" +
                        "Connection: close\r\n" +
                        "\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
#if NET5_0_OR_GREATER
                    await stream.WriteAsync(headerBytes, token);
                    await stream.WriteAsync(bodyBytes, token);
#else
                    await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                    await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
#endif
                }
            } finally {
                listener.Stop();
            }
        }

        private static (DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat) CreateSuccessfulProbeResult(DnsResolverEndpoint endpoint, int elapsedMs) {
            DnsRequestFormat requestFormat = endpoint.Transport switch {
                Transport.Tcp => DnsRequestFormat.DnsOverTCP,
                Transport.Dot => DnsRequestFormat.DnsOverTLS,
                Transport.Quic => DnsRequestFormat.DnsOverQuic,
                Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
                Transport.Multicast => DnsRequestFormat.Multicast,
                Transport.Doh => DnsRequestFormat.DnsOverHttps,
                _ => DnsRequestFormat.DnsOverUDP
            };

            return (
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport
                },
                TimeSpan.FromMilliseconds(elapsedMs),
                $"{endpoint.Host}:{endpoint.Port}",
                requestFormat
            );
        }

        /// <summary>
        /// Ensures resolver files can auto-enable probe mode and import multiple custom endpoints.
        /// </summary>
        [Fact]
        public async Task ResolverFile_AutoEnablesProbeAndLoadsCustomEndpoints() {
            string resolverFile = Path.GetTempFileName();
            File.WriteAllText(
                resolverFile,
                "# comment\r\n\r\nudp@1.1.1.1:53,tcp@1.0.0.1:53\r\n");

            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult(CreateSuccessfulProbeResult(endpoint, endpoint.Transport == Transport.Tcp ? 9 : 5)));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync("--resolver-file", resolverFile, "--probe-summary-only");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Probe:", text);
                Assert.Contains("Domain: example.com", text);
                Assert.Contains("Endpoint profile: custom", text);
                Assert.Contains("Candidates: 2", text);
                Assert.Contains("Successful probes: 2/2", text);
                Assert.Contains("Recommended endpoint: udp@1.1.1.1:53 in 5 ms via Udp", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
                File.Delete(resolverFile);
            }
        }

        /// <summary>
        /// Ensures resolver URLs can supply custom benchmark candidates.
        /// </summary>
        [Fact]
        public async Task ResolverUrl_BenchmarkLoadsCustomEndpoints() {
            int port = TestUtilities.GetFreeTcpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunHttpServerAsync(port, "// comment\r\nudp@9.9.9.9:53,doh@https://dns.example/dns-query\r\n", cts.Token);

            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult(CreateSuccessfulProbeResult(endpoint, endpoint.Transport == Transport.Doh ? 7 : 11)));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);

                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--resolver-url", $"http://127.0.0.1:{port}/resolvers.txt",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "1",
                    "--benchmark-summary-only");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Benchmark:", text);
                Assert.Contains("Endpoint profile: custom", text);
                Assert.Contains("Candidates: 2", text);
                Assert.Contains("Queries per candidate: 1", text);
                Assert.Contains("Successful candidates: 2/2", text);
                Assert.Contains("Best endpoint: doh@https://dns.example/dns-query in 7 ms average (100% success)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures probe workflows can persist scored resolver health data to JSON.
        /// </summary>
        [Fact]
        public async Task ResolverFile_ProbeSaveWritesSnapshot() {
            string resolverFile = Path.GetTempFileName();
            string snapshotFile = Path.GetTempFileName();
            File.WriteAllText(
                resolverFile,
                "udp@1.1.1.1:53,tcp@1.0.0.1:53\r\n");

            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult(CreateSuccessfulProbeResult(endpoint, endpoint.Transport == Transport.Tcp ? 9 : 5)));

            try {
                int exitCode = await InvokeCliAsync("--resolver-file", resolverFile, "--probe-summary-only", "--probe-save", snapshotFile);

                Assert.Equal(0, exitCode);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(snapshotFile));
                JsonElement root = document.RootElement;
                JsonElement summary = root.GetProperty("Summary");
                Assert.Equal("Probe", summary.GetProperty("Mode").GetString());
                Assert.Equal("udp@1.1.1.1:53", summary.GetProperty("RecommendedTarget").GetString());
                Assert.True(summary.GetProperty("RecommendationAvailable").GetBoolean());

                JsonElement results = root.GetProperty("Results");
                Assert.Equal(2, results.GetArrayLength());
                Assert.Equal("udp@1.1.1.1:53", results[0].GetProperty("Target").GetString());
                Assert.Equal(1, results[0].GetProperty("Rank").GetInt32());
                Assert.True(results[0].GetProperty("IsRecommended").GetBoolean());
            } finally {
                SetProbeEndpointOverride(null);
                File.Delete(resolverFile);
                File.Delete(snapshotFile);
            }
        }

        /// <summary>
        /// Ensures benchmark workflows can persist scored resolver recommendation data to JSON.
        /// </summary>
        [Fact]
        public async Task ResolverFile_BenchmarkSaveWritesSnapshot() {
            string resolverFile = Path.GetTempFileName();
            string snapshotFile = Path.GetTempFileName();
            File.WriteAllText(
                resolverFile,
                "udp@9.9.9.9:53,doh@https://dns.example/dns-query\r\n");

            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult(CreateSuccessfulProbeResult(endpoint, endpoint.Transport == Transport.Doh ? 7 : 11)));

            try {
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--resolver-file", resolverFile,
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "1",
                    "--benchmark-summary-only",
                    "--benchmark-save", snapshotFile);

                Assert.Equal(0, exitCode);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(snapshotFile));
                JsonElement root = document.RootElement;
                JsonElement summary = root.GetProperty("Summary");
                Assert.Equal("Benchmark", summary.GetProperty("Mode").GetString());
                Assert.Equal("doh@https://dns.example/dns-query", summary.GetProperty("RecommendedTarget").GetString());
                Assert.True(summary.GetProperty("RecommendationAvailable").GetBoolean());
                Assert.Equal(2, summary.GetProperty("CandidateCount").GetInt32());

                JsonElement results = root.GetProperty("Results");
                Assert.Equal(2, results.GetArrayLength());
                Assert.Equal("doh@https://dns.example/dns-query", results[0].GetProperty("Target").GetString());
                Assert.Equal(1, results[0].GetProperty("Rank").GetInt32());
                Assert.Equal(100, results[0].GetProperty("SuccessPercent").GetInt32());
            } finally {
                SetProbeEndpointOverride(null);
                File.Delete(resolverFile);
                File.Delete(snapshotFile);
            }
        }
    }
}
