using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests CLI diagnostics output for explain and trace modes.
    /// </summary>
    [Collection("NoParallel")]
    public class CliExplainTraceTests {
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

        private static async Task RunUdpServerAsync(int port, byte[] response, CancellationToken token) {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            UdpReceiveResult result;
#if NET5_0_OR_GREATER
            result = await udp.ReceiveAsync(token);
#else
            var receiveTask = udp.ReceiveAsync();
            var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, token));
            if (completed != receiveTask) {
                throw new OperationCanceledException(token);
            }
            result = receiveTask.Result;
#endif
#if NET5_0_OR_GREATER
            await udp.SendAsync(response, result.RemoteEndPoint, token);
#else
            await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
#endif
        }

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

        private static DnsRequestFormat GetRequestFormat(DnsEndpoint endpoint) {
            using var client = new ClientX(endpoint);
            return client.EndpointConfiguration.RequestFormat;
        }

        /// <summary>
        /// Ensures the CLI explain mode prints resolver diagnostics for a successful query.
        /// </summary>
        [Fact]
        public async Task ExplainOption_PrintsDiagnostics() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, CreateDnsHeader(), cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--explain", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Explain:", text);
                Assert.Contains("Operation: query", text);
                Assert.Contains("Resolver:", text);
                Assert.Contains("Actual transport: Udp", text);
                Assert.Contains("Cache enabled: False", text);
                Assert.Contains("Attempts recorded: 1", text);
                Assert.Contains("Final source: network", text);
                Assert.Contains("DNSSEC requested: False", text);
                Assert.Contains("Elapsed:", text);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures the CLI trace mode prints audit-oriented diagnostic details.
        /// </summary>
        [Fact]
        public async Task TraceOption_PrintsAuditDetails() {
            int port = TestUtilities.GetFreeUdpPort();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task serverTask = RunUdpServerAsync(port, CreateDnsHeader(), cts.Token);

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                SystemInformation.SetDnsServerProvider(() => new List<string> { "127.0.0.1" });
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", port.ToString());

                int exitCode = await InvokeCliAsync("--trace", "example.com");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Explain:", text);
                Assert.Contains("Trace:", text);
                Assert.Contains("Audit entries:", text);
                Assert.Contains("Used transport: Udp", text);
                Assert.Contains("Attempt 1: example.com A via DnsOverUDP/Udp", text);
            } finally {
                Console.SetOut(originalOut);
                Environment.SetEnvironmentVariable("DNSCLIENTX_CLI_PORT", null);
                SystemInformation.SetDnsServerProvider(null);
            }

            await serverTask;
        }

        /// <summary>
        /// Ensures the CLI explain formatter summarizes retry reasons and resolver attempts.
        /// </summary>
        [Fact]
        public async Task WriteExplain_PrintsRetrySummary() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP) { EnableAudit = true };
            int calls = 0;
            client.ResolverOverride = (name, type, ct) => {
                calls++;
                return Task.FromResult(new DnsResponse {
                    Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name, RequestFormat = DnsRequestFormat.DnsOverUDP } },
                    Status = calls == 1 ? DnsResponseCode.ServerFailure : DnsResponseCode.NoError,
                    UsedTransport = Transport.Udp
                });
            };

            DnsResponse response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: true, maxRetries: 2, retryDelayMs: 1);

            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo writeExplain = programType.GetMethod("WriteExplain", BindingFlags.NonPublic | BindingFlags.Static)!;

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                writeExplain.Invoke(null, new object?[] {
                    client,
                    response,
                    TimeSpan.FromMilliseconds(5),
                    "query",
                    "example.com",
                    DnsRecordType.A,
                    false,
                    false,
                    false,
                    null,
                    null
                });
            } finally {
                Console.SetOut(originalOut);
            }

            string text = output.ToString();
            Assert.Contains("Resolvers tried:", text);
            Assert.Contains("Retry reasons:", text);
            Assert.Contains("transient response", text);
        }

        /// <summary>
        /// Ensures the CLI trace formatter includes retry reasons for earlier attempts.
        /// </summary>
        [Fact]
        public async Task WriteTrace_PrintsRetryReason() {
            using var client = new ClientX("127.0.0.1", DnsRequestFormat.DnsOverUDP) { EnableAudit = true };
            int calls = 0;
            client.ResolverOverride = (name, type, ct) => {
                calls++;
                return Task.FromResult(new DnsResponse {
                    Questions = new[] { new DnsQuestion { Name = name, Type = type, OriginalName = name, RequestFormat = DnsRequestFormat.DnsOverUDP } },
                    Status = calls == 1 ? DnsResponseCode.ServerFailure : DnsResponseCode.NoError,
                    UsedTransport = Transport.Udp
                });
            };

            DnsResponse response = await client.Resolve("example.com", DnsRecordType.A, retryOnTransient: true, maxRetries: 2, retryDelayMs: 1);

            var assembly = Assembly.Load("DnsClientX.Cli");
            Type programType = assembly.GetType("DnsClientX.Cli.Program")!;
            MethodInfo writeTrace = programType.GetMethod("WriteTrace", BindingFlags.NonPublic | BindingFlags.Static)!;

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                writeTrace.Invoke(null, new object[] { client, response });
            } finally {
                Console.SetOut(originalOut);
            }

            string text = output.ToString();
            Assert.Contains("Attempt 1:", text);
            Assert.Contains("Attempt 2:", text);
            Assert.Contains("retry: transient response", text);
        }

        /// <summary>
        /// Ensures probe mode prints the selected profile, default domain, and successful variant summary.
        /// </summary>
        [Fact]
        public async Task ProbeOption_PrintsCapabilitySummary() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint == DnsEndpoint.CloudflareQuic ? Transport.Quic : Transport.Doh,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = "1.1.1.1"
                        }
                    }
                },
                TimeSpan.FromMilliseconds(12),
                $"{endpoint}.test:443",
                GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync("--probe", "-e", "Cloudflare");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Probe:", text);
                Assert.Contains("Domain: example.com", text);
                Assert.Contains("Endpoint profile: Cloudflare", text);
                Assert.Contains("Candidates: 6", text);
                Assert.Contains("[OK] Cloudflare via DnsOverHttpsJSON", text);
                Assert.Contains("[OK] CloudflareQuic via DnsOverQuic", text);
                Assert.Contains("Successful probes: 6/6", text);
                Assert.Contains("Fastest success:", text);
                Assert.Contains("Fastest consensus responder:", text);
                Assert.Contains("Transport coverage: Doh 5/5 | Quic 1/1", text);
                Assert.Contains("Answer consensus: 6/6 successful probes agree", text);
                Assert.Contains("Mismatched responders: none", text);
                Assert.Contains("Distinct answer sets: 1", text);
                Assert.Contains("Answer variants: [1] example.com A 1.1.1.1 <-", text);
                Assert.Contains("Recommended endpoint: Cloudflare in 12 ms via Doh", text);
                Assert.Contains("Policy result: pass", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe mode returns a failure exit code when every candidate probe fails.
        /// </summary>
        [Fact]
        public async Task ProbeOption_ReturnsFailureWhenAllCandidatesFail() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.ServerFailure,
                    Error = "probe failed",
                    UsedTransport = endpoint == DnsEndpoint.SystemTcp ? Transport.Tcp : Transport.Udp
                },
                TimeSpan.FromMilliseconds(8),
                $"{endpoint}.test:53",
                GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync("--probe", "-e", "System");

                Assert.Equal(1, exitCode);
                string text = output.ToString();
                Assert.Contains("[FAIL] System via DnsOverUDP", text);
                Assert.Contains("[FAIL] SystemTcp via DnsOverTCP", text);
                Assert.Contains("Failed probes: 2", text);
                Assert.Contains("Fastest success: none", text);
                Assert.Contains("Fastest consensus responder: none", text);
                Assert.Contains("Transport coverage: Tcp 0/1 | Udp 0/1", text);
                Assert.Contains("Answer consensus: none", text);
                Assert.Contains("Mismatched responders: none", text);
                Assert.Contains("Distinct answer sets: 0", text);
                Assert.Contains("Answer variants: none", text);
                Assert.Contains("Recommended endpoint: none", text);
                Assert.Contains("Policy result: fail (no successful probes)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures benchmark mode compares multiple built-in endpoints across domains, record types, and repeated attempts.
        /// </summary>
        [Fact]
        public async Task BenchmarkOption_PrintsRankedSummaryAcrossEndpoints() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint == DnsEndpoint.Quad9 ? Transport.Tcp : Transport.Doh,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint == DnsEndpoint.Quad9 ? "9.9.9.9" : "1.1.1.1"
                        }
                    }
                },
                endpoint == DnsEndpoint.Quad9 ? TimeSpan.FromMilliseconds(20) : TimeSpan.FromMilliseconds(10),
                $"{endpoint}.test:443",
                endpoint == DnsEndpoint.Quad9 ? DnsRequestFormat.DnsOverTCP : GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--endpoint", "Cloudflare,Quad9",
                    "--domain", "example.com,microsoft.com",
                    "--type", "A,AAAA",
                    "--benchmark-attempts", "2");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Benchmark:", text);
                Assert.Contains("Domains: example.com, microsoft.com", text);
                Assert.Contains("Types: A, AAAA", text);
                Assert.Contains("Attempts per combination: 2", text);
                Assert.Contains("Timeout (ms): 2000", text);
                Assert.Contains("Concurrency: 4", text);
                Assert.Contains("Candidates: 2", text);
                Assert.Contains("Queries per candidate: 8", text);
                Assert.Contains("[OK] Cloudflare", text);
                Assert.Contains("[OK] Quad9", text);
                Assert.Contains("Ranked 1: Cloudflare avg 10 ms, success 100% (8/8), resolver Cloudflare.test:443", text);
                Assert.Contains("Ranked 2: Quad9 avg 20 ms, success 100% (8/8), resolver Quad9.test:443", text);
                Assert.Contains("Best endpoint: Cloudflare in 10 ms average (100% success)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures benchmark mode emits a stable machine-readable summary line.
        /// </summary>
        [Fact]
        public async Task BenchmarkOption_SummaryLine_PrintsStableKeyValueOutput() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint.Host == "1.1.1.1" ? "1.1.1.1" : "9.9.9.9"
                        }
                    }
                },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--benchmark-summary-line",
                    "--probe-endpoint", "udp@1.1.1.1:53,tcp@9.9.9.9:53",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "2");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("BENCHMARK_SUMMARY", text);
                Assert.Contains("summary_version=1", text);
                Assert.Contains("result=pass", text);
                Assert.Contains("exit_code=0", text);
                Assert.Contains("candidates=2", text);
                Assert.Contains("successful_candidates=2", text);
                Assert.Contains("total_queries=4", text);
                Assert.Contains("successful_queries=4", text);
                Assert.Contains("timeout_ms=2000", text);
                Assert.Contains("concurrency=4", text);
                Assert.Contains("best_target=udp_1_1_1_1_53", text);
                Assert.Contains("best_resolver=1_1_1_1_53", text);
                Assert.Contains("best_transport=udp", text);
                Assert.Contains("best_avg_ms=5", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures benchmark mode supports summary-only output for automation-friendly logs.
        /// </summary>
        [Fact]
        public async Task BenchmarkOption_SummaryOnly_SuppressesPerCandidateRows() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint == DnsEndpoint.Quad9 ? Transport.Tcp : Transport.Doh,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint == DnsEndpoint.Quad9 ? "9.9.9.9" : "1.1.1.1"
                        }
                    }
                },
                endpoint == DnsEndpoint.Quad9 ? TimeSpan.FromMilliseconds(20) : TimeSpan.FromMilliseconds(10),
                $"{endpoint}.test:443",
                endpoint == DnsEndpoint.Quad9 ? DnsRequestFormat.DnsOverTCP : GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--benchmark-summary-only",
                    "--endpoint", "Cloudflare,Quad9",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "1");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Benchmark:", text);
                Assert.Contains("Detail mode: summary-only", text);
                Assert.Contains("Benchmark Summary:", text);
                Assert.DoesNotContain("[OK] Cloudflare", text);
                Assert.DoesNotContain("[OK] Quad9", text);
                Assert.Contains("Ranked 1: Cloudflare avg 10 ms, success 100% (1/1), resolver Cloudflare.test:443", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures benchmark mode respects the configured concurrency limit.
        /// </summary>
        [Fact]
        public async Task BenchmarkOption_RespectsConcurrencyLimit() {
            int active = 0;
            int maxActive = 0;

            SetProbeOverride(async (endpoint, domain, ct) => {
                int current = Interlocked.Increment(ref active);
                int observed;
                while ((observed = maxActive) < current && Interlocked.CompareExchange(ref maxActive, current, observed) != observed) {
                }

                try {
                    await Task.Delay(25, ct);
                    return (
                        new DnsResponse {
                            Status = DnsResponseCode.NoError,
                            UsedTransport = Transport.Doh,
                            Answers = new[] {
                                new DnsAnswer {
                                    Name = domain,
                                    Type = DnsRecordType.A,
                                    TTL = 60,
                                    DataRaw = "1.1.1.1"
                                }
                            }
                        },
                        TimeSpan.FromMilliseconds(25),
                        $"{endpoint}.test:443",
                        GetRequestFormat(endpoint)
                    );
                } finally {
                    Interlocked.Decrement(ref active);
                }
            });

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--endpoint", "Cloudflare",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "4",
                    "--benchmark-concurrency", "2",
                    "--benchmark-timeout", "1500");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Timeout (ms): 1500", text);
                Assert.Contains("Concurrency: 2", text);
                Assert.True(maxActive <= 2, $"Expected benchmark concurrency to stay at or below 2, but observed {maxActive}.");
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures benchmark mode can fail on overall query success rate for automation scenarios.
        /// </summary>
        [Fact]
        public async Task BenchmarkOption_FailsWhenSuccessRateBelowPolicy() {
            int calls = 0;

            SetProbeEndpointOverride((endpoint, domain, ct) => {
                int call = Interlocked.Increment(ref calls);
                bool succeeded = call <= 2;

                return Task.FromResult((
                    new DnsResponse {
                        Status = succeeded ? DnsResponseCode.NoError : DnsResponseCode.ServerFailure,
                        Error = succeeded ? string.Empty : "timeout",
                        UsedTransport = endpoint.Transport,
                        Answers = succeeded
                            ? new[] {
                                new DnsAnswer {
                                    Name = domain,
                                    Type = DnsRecordType.A,
                                    TTL = 60,
                                    DataRaw = "1.1.1.1"
                                }
                            }
                            : Array.Empty<DnsAnswer>()
                    },
                    TimeSpan.FromMilliseconds(succeeded ? 5 : 50),
                    $"{endpoint.Host}:{endpoint.Port}",
                    endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
                ));
            });

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--benchmark-summary-line",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "4",
                    "--benchmark-min-success-percent", "75");

                Assert.Equal(3, exitCode);
                string text = output.ToString();
                Assert.Contains("Successful queries: 2/4", text);
                Assert.Contains("Successful query rate: 50%", text);
                Assert.Contains("Policy result: fail (success rate 50% below required 75%)", text);
                Assert.Contains("BENCHMARK_SUMMARY", text);
                Assert.Contains("result=fail", text);
                Assert.Contains("exit_code=3", text);
                Assert.Contains("success_percent=50", text);
                Assert.Contains("policy_result=fail", text);
                Assert.Contains("policy_reason=success_rate_50_below_required_75", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures benchmark mode can require multiple healthy candidates.
        /// </summary>
        [Fact]
        public async Task BenchmarkOption_FailsWhenSuccessfulCandidateCountBelowPolicy() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = endpoint == DnsEndpoint.Quad9 ? DnsResponseCode.ServerFailure : DnsResponseCode.NoError,
                    Error = endpoint == DnsEndpoint.Quad9 ? "unreachable" : string.Empty,
                    UsedTransport = endpoint == DnsEndpoint.Quad9 ? Transport.Tcp : Transport.Doh,
                    Answers = endpoint == DnsEndpoint.Quad9
                        ? Array.Empty<DnsAnswer>()
                        : new[] {
                            new DnsAnswer {
                                Name = domain,
                                Type = DnsRecordType.A,
                                TTL = 60,
                                DataRaw = "1.1.1.1"
                            }
                        }
                },
                endpoint == DnsEndpoint.Quad9 ? TimeSpan.FromMilliseconds(90) : TimeSpan.FromMilliseconds(10),
                $"{endpoint}.test:443",
                endpoint == DnsEndpoint.Quad9 ? DnsRequestFormat.DnsOverTCP : GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--benchmark",
                    "--benchmark-summary-line",
                    "--endpoint", "Cloudflare,Quad9",
                    "--domain", "example.com",
                    "--type", "A",
                    "--benchmark-attempts", "1",
                    "--benchmark-min-successful-candidates", "2");

                Assert.Equal(3, exitCode);
                string text = output.ToString();
                Assert.Contains("Successful candidates: 1/2", text);
                Assert.Contains("Policy result: fail (successful candidates 1/2 below required count 2)", text);
                Assert.Contains("BENCHMARK_SUMMARY", text);
                Assert.Contains("result=fail", text);
                Assert.Contains("exit_code=3", text);
                Assert.Contains("successful_candidates=1", text);
                Assert.Contains("policy_result=fail", text);
                Assert.Contains("policy_reason=successful_candidates_1_2_below_required_count_2", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe mode accepts explicit custom endpoint transports.
        /// </summary>
        [Fact]
        public async Task ProbeOption_CustomEndpoint_PrintsTransportSummary() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport
                },
                TimeSpan.FromMilliseconds(9),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport switch {
                    Transport.Tcp => DnsRequestFormat.DnsOverTCP,
                    Transport.Dot => DnsRequestFormat.DnsOverTLS,
                    Transport.Quic => DnsRequestFormat.DnsOverQuic,
                    Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
                    Transport.Multicast => DnsRequestFormat.Multicast,
                    _ => DnsRequestFormat.DnsOverUDP
                }
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync("--probe", "--probe-endpoint", "tcp@1.1.1.1:53");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Endpoint profile: custom", text);
                Assert.Contains("Candidates: 1", text);
                Assert.Contains("[OK] tcp@1.1.1.1:53 via DnsOverTCP", text);
                Assert.Contains("Successful probes: 1/1", text);
                Assert.Contains("Fastest success: tcp@1.1.1.1:53", text);
                Assert.Contains("Fastest consensus responder: tcp@1.1.1.1:53", text);
                Assert.Contains("Transport coverage: Tcp 1/1", text);
                Assert.Contains("Answer consensus: 1/1 successful probes agree", text);
                Assert.Contains("Mismatched responders: none", text);
                Assert.Contains("Distinct answer sets: 1", text);
                Assert.Contains("Answer variants: [1] (no answers) <- tcp@1.1.1.1:53", text);
                Assert.Contains("Recommended endpoint: tcp@1.1.1.1:53 in 9 ms via Tcp", text);
                Assert.Contains("Policy result: pass", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe summary-only mode keeps the aggregate output but suppresses per-endpoint rows.
        /// </summary>
        [Fact]
        public async Task ProbeOption_SummaryOnly_SuppressesPerEndpointRows() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint == DnsEndpoint.CloudflareQuic ? Transport.Quic : Transport.Doh,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = "1.1.1.1"
                        }
                    }
                },
                TimeSpan.FromMilliseconds(12),
                $"{endpoint}.test:443",
                GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync("--probe", "--probe-summary-only", "-e", "Cloudflare");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Probe:", text);
                Assert.Contains("Detail mode: summary-only", text);
                Assert.Contains("Probe Summary:", text);
                Assert.Contains("Successful probes: 6/6", text);
                Assert.Contains("Policy result: pass", text);
                Assert.DoesNotContain("[OK] Cloudflare via", text);
                Assert.DoesNotContain("[OK] CloudflareQuic via", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe summary-line mode emits a stable machine-readable line for successful runs.
        /// </summary>
        [Fact]
        public async Task ProbeOption_SummaryLine_PrintsStableKeyValueOutput() {
            SetProbeOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint == DnsEndpoint.CloudflareQuic ? Transport.Quic : Transport.Doh,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = "1.1.1.1"
                        }
                    }
                },
                TimeSpan.FromMilliseconds(12),
                $"{endpoint}.test:443",
                GetRequestFormat(endpoint)
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync("--probe", "--probe-summary-only", "--probe-summary-line", "-e", "Cloudflare");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("PROBE_SUMMARY", text);
                Assert.Contains("summary_version=1", text);
                Assert.Contains("result=pass", text);
                Assert.Contains("exit_code=0", text);
                Assert.Contains("successful=6", text);
                Assert.Contains("total=6", text);
                Assert.Contains("success_percent=100", text);
                Assert.Contains("consensus_count=6", text);
                Assert.Contains("consensus_total=6", text);
                Assert.Contains("consensus_percent=100", text);
                Assert.Contains("distinct_answer_sets=1", text);
                Assert.Contains("fastest_success_target=cloudflare", text);
                Assert.Contains("fastest_success_transport=doh", text);
                Assert.Contains("fastest_success_ms=12", text);
                Assert.Contains("fastest_consensus_target=cloudflare", text);
                Assert.Contains("fastest_consensus_transport=doh", text);
                Assert.Contains("fastest_consensus_ms=12", text);
                Assert.Contains("recommended_target=cloudflare", text);
                Assert.Contains("recommended_resolver=cloudflare_test_443", text);
                Assert.Contains("recommended_transport=doh", text);
                Assert.Contains("recommended_ms=12", text);
                Assert.Contains("recommended_status=selected", text);
                Assert.Contains("recommendation_source=unanimous_agreement", text);
                Assert.Contains("why_not_recommended=none", text);
                Assert.Contains("policy_reason=none", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeOverride(null);
            }
        }

        /// <summary>
        /// Ensures the machine-readable summary identifies single-success fallback recommendations.
        /// </summary>
        [Fact]
        public async Task ProbeOption_SummaryLine_UsesSingleSuccessRecommendationSource() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport
                },
                TimeSpan.FromMilliseconds(9),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport switch {
                    Transport.Tcp => DnsRequestFormat.DnsOverTCP,
                    Transport.Dot => DnsRequestFormat.DnsOverTLS,
                    Transport.Quic => DnsRequestFormat.DnsOverQuic,
                    Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
                    Transport.Multicast => DnsRequestFormat.Multicast,
                    _ => DnsRequestFormat.DnsOverUDP
                }
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync("--probe", "--probe-summary-only", "--probe-summary-line", "--probe-endpoint", "tcp@1.1.1.1:53");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("recommended_target=tcp_1_1_1_1_53", text);
                Assert.Contains("recommended_resolver=1_1_1_1_53", text);
                Assert.Contains("recommended_transport=tcp", text);
                Assert.Contains("recommended_ms=9", text);
                Assert.Contains("recommended_status=selected", text);
                Assert.Contains("recommendation_source=single_success_fallback", text);
                Assert.Contains("why_not_recommended=none", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures multi-endpoint custom probes highlight answer mismatches in the summary.
        /// </summary>
        [Fact]
        public async Task ProbeOption_CustomEndpoints_HighlightsAnswerMismatches() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint.Host == "1.1.1.1" ? "1.1.1.1" : "1.0.0.1"
                        }
                    }
                },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "tcp@1.0.0.1:53");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Transport coverage: Tcp 1/1 | Udp 1/1", text);
                Assert.Contains("Answer consensus: 1/2 successful probes agree", text);
                Assert.Contains("Fastest consensus responder:", text);
                Assert.Contains("Mismatched responders:", text);
                Assert.DoesNotContain("Mismatched responders: none", text);
                Assert.Contains("Distinct answer sets: 2", text);
                Assert.Contains("Answer variants:", text);
                Assert.Contains("example.com A 1.1.1.1", text);
                Assert.Contains("example.com A 1.0.0.1", text);
                Assert.Contains("Recommended endpoint: none", text);
                Assert.Contains("Policy result: pass", text);
                Assert.True(
                    text.Contains("tcp@1.0.0.1:53", StringComparison.Ordinal) ||
                    text.Contains("udp@1.1.1.1:53", StringComparison.Ordinal),
                    $"Expected one mismatched responder to be reported, but output was:{Environment.NewLine}{text}");
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe summary-line mode reports policy failures in a parseable form.
        /// </summary>
        [Fact]
        public async Task ProbeOption_SummaryLine_PrintsPolicyFailureState() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                endpoint.Host == "1.1.1.1"
                    ? new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        UsedTransport = endpoint.Transport,
                        Answers = new[] {
                            new DnsAnswer {
                                Name = domain,
                                Type = DnsRecordType.A,
                                TTL = 60,
                                DataRaw = "1.1.1.1"
                            }
                        }
                    }
                    : new DnsResponse {
                        Status = DnsResponseCode.ServerFailure,
                        Error = "probe failed",
                        UsedTransport = endpoint.Transport
                    },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-summary-only",
                    "--probe-summary-line",
                    "--probe-min-success", "2",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "udp@1.0.0.1:53",
                    "--probe-endpoint", "tcp@1.0.0.2:53");

                Assert.Equal(3, exitCode);
                string text = output.ToString();
                Assert.Contains("PROBE_SUMMARY", text);
                Assert.Contains("result=fail", text);
                Assert.Contains("exit_code=3", text);
                Assert.Contains("successful=1", text);
                Assert.Contains("total=3", text);
                Assert.Contains("success_percent=33", text);
                Assert.Contains("consensus_count=1", text);
                Assert.Contains("consensus_total=1", text);
                Assert.Contains("consensus_percent=100", text);
                Assert.Contains("distinct_answer_sets=1", text);
                Assert.Contains("fastest_success_target=udp_1_1_1_1_53", text);
                Assert.Contains("fastest_success_transport=udp", text);
                Assert.Contains("fastest_success_ms=5", text);
                Assert.Contains("fastest_consensus_target=udp_1_1_1_1_53", text);
                Assert.Contains("fastest_consensus_transport=udp", text);
                Assert.Contains("fastest_consensus_ms=5", text);
                Assert.Contains("recommended_target=none", text);
                Assert.Contains("recommended_resolver=none", text);
                Assert.Contains("recommended_transport=none", text);
                Assert.Contains("recommended_ms=0", text);
                Assert.Contains("recommended_status=blocked_by_policy", text);
                Assert.Contains("recommendation_source=none", text);
                Assert.Contains("why_not_recommended=policy_failed", text);
                Assert.Contains("policy_reason=successful_probes_1_3_below_required_count_2", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe recommendations are emitted when a consensus policy passes with a clear majority answer set.
        /// </summary>
        [Fact]
        public async Task ProbeOption_MinConsensus_ProducesRecommendedEndpointForClearMajority() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint.Host == "1.1.1.1" || endpoint.Host == "1.1.1.2" ? "1.1.1.1" : "1.0.0.1"
                        }
                    }
                },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) :
                endpoint.Host == "1.1.1.2" ? TimeSpan.FromMilliseconds(6) :
                TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-summary-only",
                    "--probe-summary-line",
                    "--probe-min-consensus", "60",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "udp@1.1.1.2:53",
                    "--probe-endpoint", "tcp@1.0.0.1:53");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("Recommended endpoint: udp@1.1.1.1:53 in 5 ms via Udp", text);
                Assert.Contains("recommended_target=udp_1_1_1_1_53", text);
                Assert.Contains("recommended_resolver=1_1_1_1_53", text);
                Assert.Contains("recommended_transport=udp", text);
                Assert.Contains("recommended_ms=5", text);
                Assert.Contains("recommended_status=selected", text);
                Assert.Contains("recommendation_source=consensus_policy_majority", text);
                Assert.Contains("why_not_recommended=none", text);
                Assert.Contains("policy_reason=none", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures the machine-readable summary explains why no recommendation was produced when disagreement exists without a consensus policy.
        /// </summary>
        [Fact]
        public async Task ProbeOption_SummaryLine_ExplainsMissingRecommendationWithoutConsensusPolicy() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint.Host == "1.1.1.1" || endpoint.Host == "1.1.1.2" ? "1.1.1.1" : "1.0.0.1"
                        }
                    }
                },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) :
                endpoint.Host == "1.1.1.2" ? TimeSpan.FromMilliseconds(6) :
                TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-summary-only",
                    "--probe-summary-line",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "udp@1.1.1.2:53",
                    "--probe-endpoint", "tcp@1.0.0.1:53");

                Assert.Equal(0, exitCode);
                string text = output.ToString();
                Assert.Contains("recommended_target=none", text);
                Assert.Contains("recommended_resolver=none", text);
                Assert.Contains("recommended_transport=none", text);
                Assert.Contains("recommended_ms=0", text);
                Assert.Contains("recommended_status=unavailable", text);
                Assert.Contains("recommendation_source=none", text);
                Assert.Contains("why_not_recommended=consensus_policy_not_enabled", text);
                Assert.Contains("policy_reason=none", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe mode can fail on answer disagreement for CI-friendly automation.
        /// </summary>
        [Fact]
        public async Task ProbeOption_RequireConsensus_FailsWhenRespondersDisagree() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint.Host == "1.1.1.1" ? "1.1.1.1" : "1.0.0.1"
                        }
                    }
                },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-require-consensus",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "tcp@1.0.0.1:53");

                Assert.Equal(2, exitCode);
                string text = output.ToString();
                Assert.Contains("Answer consensus: 1/2 successful probes agree", text);
                Assert.Contains("Policy result: fail (consensus required but top answer set reached 50%)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe mode can enforce a minimum consensus threshold.
        /// </summary>
        [Fact]
        public async Task ProbeOption_MinConsensus_FailsWhenThresholdIsNotMet() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = endpoint.Transport,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = domain,
                            Type = DnsRecordType.A,
                            TTL = 60,
                            DataRaw = endpoint.Host == "1.1.1.1" || endpoint.Host == "1.1.1.2" ? "1.1.1.1" : "1.0.0.1"
                        }
                    }
                },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) :
                endpoint.Host == "1.1.1.2" ? TimeSpan.FromMilliseconds(6) :
                TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-min-consensus", "80",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "udp@1.1.1.2:53",
                    "--probe-endpoint", "tcp@1.0.0.1:53");

                Assert.Equal(2, exitCode);
                string text = output.ToString();
                Assert.Contains("Answer consensus: 2/3 successful probes agree", text);
                Assert.Contains("Policy result: fail (consensus 67% below required 80%)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe mode can enforce a minimum successful responder count.
        /// </summary>
        [Fact]
        public async Task ProbeOption_MinSuccessCount_FailsWhenTooFewRespondersSucceed() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                endpoint.Host == "1.1.1.1"
                    ? new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        UsedTransport = endpoint.Transport,
                        Answers = new[] {
                            new DnsAnswer {
                                Name = domain,
                                Type = DnsRecordType.A,
                                TTL = 60,
                                DataRaw = "1.1.1.1"
                            }
                        }
                    }
                    : new DnsResponse {
                        Status = DnsResponseCode.ServerFailure,
                        Error = "probe failed",
                        UsedTransport = endpoint.Transport
                    },
                endpoint.Host == "1.1.1.1" ? TimeSpan.FromMilliseconds(5) : TimeSpan.FromMilliseconds(7),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-min-success", "2",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "udp@1.0.0.1:53",
                    "--probe-endpoint", "tcp@1.0.0.2:53");

                Assert.Equal(3, exitCode);
                string text = output.ToString();
                Assert.Contains("Successful probes: 1/3", text);
                Assert.Contains("Policy result: fail (successful probes 1/3 below required count 2)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }

        /// <summary>
        /// Ensures probe mode can enforce a minimum probe success rate.
        /// </summary>
        [Fact]
        public async Task ProbeOption_MinSuccessPercent_FailsWhenSuccessRateIsTooLow() {
            SetProbeEndpointOverride((endpoint, domain, ct) => Task.FromResult((
                endpoint.Host == "1.1.1.1" || endpoint.Host == "1.1.1.2"
                    ? new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        UsedTransport = endpoint.Transport,
                        Answers = new[] {
                            new DnsAnswer {
                                Name = domain,
                                Type = DnsRecordType.A,
                                TTL = 60,
                                DataRaw = "1.1.1.1"
                            }
                        }
                    }
                    : new DnsResponse {
                        Status = DnsResponseCode.ServerFailure,
                        Error = "probe failed",
                        UsedTransport = endpoint.Transport
                    },
                endpoint.Host == "1.1.1.3" ? TimeSpan.FromMilliseconds(7) : TimeSpan.FromMilliseconds(5),
                $"{endpoint.Host}:{endpoint.Port}",
                endpoint.Transport == Transport.Tcp ? DnsRequestFormat.DnsOverTCP : DnsRequestFormat.DnsOverUDP
            )));

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                int exitCode = await InvokeCliAsync(
                    "--probe",
                    "--probe-min-success-percent", "75",
                    "--probe-endpoint", "udp@1.1.1.1:53",
                    "--probe-endpoint", "udp@1.1.1.2:53",
                    "--probe-endpoint", "tcp@1.1.1.3:53",
                    "--probe-endpoint", "tcp@1.0.0.1:53");

                Assert.Equal(3, exitCode);
                string text = output.ToString();
                Assert.Contains("Successful probes: 2/4", text);
                Assert.Contains("Policy result: fail (success rate 50% below required 75%)", text);
            } finally {
                Console.SetOut(originalOut);
                SetProbeEndpointOverride(null);
            }
        }
    }
}
