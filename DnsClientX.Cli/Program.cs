using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static class Program {
        private sealed class CliOptions {
            public string? Domain { get; set; }
            public DnsRecordType RecordType { get; set; } = DnsRecordType.A;
            public DnsEndpoint Endpoint { get; set; } = DnsEndpoint.System;
            public bool RequestDnsSec { get; set; }
            public bool ValidateDnsSec { get; set; }
            public bool WirePost { get; set; }
            public bool Probe { get; set; }
            public List<string> ProbeEndpoints { get; } = new List<string>();
            public bool ProbeSummaryOnly { get; set; }
            public bool ProbeSummaryLine { get; set; }
            public bool ProbeRequireConsensus { get; set; }
            public int? ProbeMinConsensusPercent { get; set; }
            public int? ProbeMinSuccessCount { get; set; }
            public int? ProbeMinSuccessPercent { get; set; }
            public bool Explain { get; set; }
            public bool Trace { get; set; }
            public bool DoUpdate { get; set; }
            public string? Zone { get; set; }
            public string? UpdateName { get; set; }
            public string? UpdateData { get; set; }
            public int Ttl { get; set; } = 300;
        }

        private sealed class ProbeResult {
            public DnsEndpoint Endpoint { get; set; }
            public string? DisplayName { get; set; }
            public DnsRequestFormat RequestFormat { get; set; }
            public string Resolver { get; set; } = string.Empty;
            public DnsResponse? Response { get; set; }
            public TimeSpan Elapsed { get; set; }
            public string? Error { get; set; }

            public bool Succeeded =>
                Response != null &&
                Response.Status == DnsResponseCode.NoError &&
                string.IsNullOrWhiteSpace(Response.Error);
        }

        private sealed class ProbePolicyOutcome {
            public bool Passed { get; set; }
            public string Reason { get; set; } = "none";
            public int ExitCode { get; set; }
        }

        private static Func<DnsEndpoint, string, CancellationToken, Task<(DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat)>>? ProbeOverride = null;
        private static Func<DnsResolverEndpoint, string, CancellationToken, Task<(DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat)>>? ProbeEndpointOverride = null;

        private static async Task<int> Main(string[] args) {
            if (args.Length == 0 ||
                string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase)) {
                ShowHelp();
                return 0;
            }

            if (!TryParseArgs(args, out CliOptions? options, out string? errorMessage, out IReadOnlyList<string>? invalidSwitches)) {
                if (!string.IsNullOrEmpty(errorMessage)) {
                    Console.Error.WriteLine(errorMessage);
                }

                if (invalidSwitches != null) {
                    foreach (string invalid in invalidSwitches) {
                        Console.Error.WriteLine($"Unknown argument: {invalid}");
                    }
                }

                ShowHelp();
                return 1;
            }

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true;
                cts.Cancel();
            };

            try {
                if (options!.Probe) {
                    return await RunProbeAsync(options, cts.Token).ConfigureAwait(false);
                }

                await using var client = new ClientX(options!.Endpoint);
                ConfigureClient(client, options);

                var stopwatch = Stopwatch.StartNew();
                DnsResponse response;
                if (options.DoUpdate) {
                    response = await client.UpdateRecordAsync(options.Zone!, options.UpdateName!, options.RecordType, options.UpdateData!, options.Ttl, cts.Token).ConfigureAwait(false);
                    stopwatch.Stop();

                    Console.WriteLine($"Update status: {response.Status} (retries {response.RetryCount})");
                    if (options.Explain || options.Trace) {
                        WriteExplain(
                            client,
                            response,
                            stopwatch.Elapsed,
                            operation: "update",
                            target: options.UpdateName!,
                            recordType: options.RecordType,
                            requestDnsSec: false,
                            validateDnsSec: false,
                            trace: options.Trace,
                            zone: options.Zone,
                            ttl: options.Ttl);
                    }
                } else {
                    response = await client.Resolve(options.Domain!, options.RecordType, options.RequestDnsSec, options.ValidateDnsSec, cancellationToken: cts.Token).ConfigureAwait(false);
                    stopwatch.Stop();

                    Console.WriteLine($"Status: {response.Status} (retries {response.RetryCount})");
                    foreach (var answer in response.Answers ?? Array.Empty<DnsAnswer>()) {
                        Console.WriteLine($"{answer.Name}\t{answer.Type}\t{answer.TTL}\t{answer.Data}");
                    }

                    if (options.Explain || options.Trace) {
                        WriteExplain(
                            client,
                            response,
                            stopwatch.Elapsed,
                            operation: "query",
                            target: options.Domain!,
                            recordType: options.RecordType,
                            requestDnsSec: options.RequestDnsSec,
                            validateDnsSec: options.ValidateDnsSec,
                            trace: options.Trace);
                    }
                }

                return 0;
            } catch (OperationCanceledException) {
                Console.Error.WriteLine("Operation canceled.");
                return 1;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static bool TryParseArgs(string[] args, out CliOptions? options, out string? errorMessage, out IReadOnlyList<string>? invalidSwitches) {
            options = new CliOptions();
            errorMessage = null;
            var unknown = new List<string>();

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                switch (arg) {
                    case var opt when opt.Equals("-t", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--type", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--type", out string? recordTypeValue, out errorMessage) ||
                            !Enum.TryParse(recordTypeValue, true, out DnsRecordType recordType)) {
                            errorMessage ??= $"Invalid value for --type: {recordTypeValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.RecordType = recordType;
                        break;
                    case var opt when opt.Equals("-e", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--endpoint", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--endpoint", out string? endpointValue, out errorMessage) ||
                            !Enum.TryParse(endpointValue, true, out DnsEndpoint endpoint)) {
                            errorMessage ??= $"Invalid value for --endpoint: {endpointValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Endpoint = endpoint;
                        break;
                    case var opt when opt.Equals("--dnssec", StringComparison.OrdinalIgnoreCase):
                        options.RequestDnsSec = true;
                        break;
                    case var opt when opt.Equals("--validate-dnssec", StringComparison.OrdinalIgnoreCase):
                        options.ValidateDnsSec = true;
                        break;
                    case var opt when opt.Equals("--wire-post", StringComparison.OrdinalIgnoreCase):
                        options.WirePost = true;
                        break;
                    case var opt when opt.Equals("--probe", StringComparison.OrdinalIgnoreCase):
                        options.Probe = true;
                        break;
                    case var opt when opt.Equals("--probe-endpoint", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--probe-endpoint", out string? probeEndpoint, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Probe = true;
                        options.ProbeEndpoints.Add(probeEndpoint!);
                        break;
                    case var opt when opt.Equals("--probe-summary-only", StringComparison.OrdinalIgnoreCase):
                        options.Probe = true;
                        options.ProbeSummaryOnly = true;
                        break;
                    case var opt when opt.Equals("--probe-summary-line", StringComparison.OrdinalIgnoreCase):
                        options.Probe = true;
                        options.ProbeSummaryLine = true;
                        break;
                    case var opt when opt.Equals("--probe-require-consensus", StringComparison.OrdinalIgnoreCase):
                        options.Probe = true;
                        options.ProbeRequireConsensus = true;
                        break;
                    case var opt when opt.Equals("--probe-min-consensus", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--probe-min-consensus", out string? minConsensusValue, out errorMessage) ||
                            !int.TryParse(minConsensusValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minConsensusPercent)) {
                            errorMessage ??= $"Invalid value for --probe-min-consensus: {minConsensusValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (minConsensusPercent < 1 || minConsensusPercent > 100) {
                            errorMessage = $"--probe-min-consensus must be between 1 and 100: {minConsensusPercent}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Probe = true;
                        options.ProbeMinConsensusPercent = minConsensusPercent;
                        break;
                    case var opt when opt.Equals("--probe-min-success", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--probe-min-success", out string? minSuccessValue, out errorMessage) ||
                            !int.TryParse(minSuccessValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minSuccessCount)) {
                            errorMessage ??= $"Invalid value for --probe-min-success: {minSuccessValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (minSuccessCount < 1) {
                            errorMessage = $"--probe-min-success must be at least 1: {minSuccessCount}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Probe = true;
                        options.ProbeMinSuccessCount = minSuccessCount;
                        break;
                    case var opt when opt.Equals("--probe-min-success-percent", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--probe-min-success-percent", out string? minSuccessPercentValue, out errorMessage) ||
                            !int.TryParse(minSuccessPercentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minSuccessPercent)) {
                            errorMessage ??= $"Invalid value for --probe-min-success-percent: {minSuccessPercentValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (minSuccessPercent < 1 || minSuccessPercent > 100) {
                            errorMessage = $"--probe-min-success-percent must be between 1 and 100: {minSuccessPercent}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Probe = true;
                        options.ProbeMinSuccessPercent = minSuccessPercent;
                        break;
                    case var opt when opt.Equals("--explain", StringComparison.OrdinalIgnoreCase):
                        options.Explain = true;
                        break;
                    case var opt when opt.Equals("--trace", StringComparison.OrdinalIgnoreCase):
                        options.Trace = true;
                        options.Explain = true;
                        break;
                    case var opt when opt.Equals("--update", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--update", out string? zone, out errorMessage) ||
                            !TryReadNext(args, ref i, "--update", out string? updateName, out errorMessage) ||
                            !TryReadNext(args, ref i, "--update", out string? updateTypeValue, out errorMessage) ||
                            !TryReadNext(args, ref i, "--update", out string? updateData, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (!Enum.TryParse(updateTypeValue, true, out DnsRecordType updateType)) {
                            errorMessage = $"Invalid value for --update type: {updateTypeValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.DoUpdate = true;
                        options.Zone = zone;
                        options.UpdateName = updateName;
                        options.RecordType = updateType;
                        options.UpdateData = updateData;
                        break;
                    case var opt when opt.Equals("--ttl", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--ttl", out string? ttlValue, out errorMessage) ||
                            !int.TryParse(ttlValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ttl)) {
                            errorMessage ??= $"Invalid value for --ttl: {ttlValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Ttl = ttl;
                        break;
                    default:
                        if (options.Domain is null && !arg.StartsWith("-", StringComparison.Ordinal)) {
                            options.Domain = arg;
                        } else {
                            unknown.Add(arg);
                        }
                        break;
                }
            }

            if (unknown.Count > 0) {
                invalidSwitches = unknown;
                options = null;
                return false;
            }

            if (options.DoUpdate && options.Probe) {
                errorMessage = "--probe cannot be combined with --update.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.DoUpdate) {
                if (options.Zone is null || options.UpdateName is null || options.UpdateData is null) {
                    errorMessage = "Invalid --update arguments.";
                    invalidSwitches = null;
                    options = null;
                    return false;
                }
            } else if (!options.Probe && string.IsNullOrWhiteSpace(options.Domain)) {
                errorMessage = "Domain name is required.";
                invalidSwitches = null;
                options = null;
                return false;
            } else if (options.Probe && string.IsNullOrWhiteSpace(options.Domain)) {
                options.Domain = "example.com";
            }

            invalidSwitches = Array.Empty<string>();
            return true;
        }

        private static bool TryReadNext(string[] args, ref int index, string optionName, out string? value, out string? errorMessage) {
            if (index + 1 >= args.Length) {
                value = null;
                errorMessage = $"Missing value for {optionName}";
                return false;
            }

            value = args[++index];
            errorMessage = null;
            return true;
        }

        private static void ConfigureClient(ClientX client, CliOptions options) {
            client.EnableAudit = options.Explain || options.Trace;

            string? envPort = Environment.GetEnvironmentVariable("DNSCLIENTX_CLI_PORT");
            if (int.TryParse(envPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out int customPort) && customPort > 0) {
                client.EndpointConfiguration.Port = customPort;
            }

            if (options.WirePost &&
                (client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps ||
                 client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                 client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON ||
                 client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST)) {
                client.EndpointConfiguration.RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
            }
        }

        private static async Task<int> RunProbeAsync(CliOptions options, CancellationToken cancellationToken) {
            string domain = options.Domain!;
            if (options.ProbeEndpoints.Count > 0) {
                return await RunCustomProbeAsync(options, domain, cancellationToken).ConfigureAwait(false);
            }

            DnsEndpoint[] plan = BuildProbePlan(options.Endpoint);

            Console.WriteLine("Probe:");
            Console.WriteLine($"  Domain: {domain}");
            Console.WriteLine($"  Endpoint profile: {options.Endpoint}");
            Console.WriteLine($"  Candidates: {plan.Length}");
            Console.WriteLine($"  Detail mode: {(options.ProbeSummaryOnly ? "summary-only" : "full")}");

            var results = new List<ProbeResult>(plan.Length);
            foreach (DnsEndpoint endpoint in plan) {
                cancellationToken.ThrowIfCancellationRequested();
                ProbeResult result = await ProbeEndpointAsync(endpoint, domain, options, cancellationToken).ConfigureAwait(false);
                results.Add(result);
                if (!options.ProbeSummaryOnly) {
                    WriteProbeResult(result);
                }
            }

            int successCount = results.Count(result => result.Succeeded);
            Console.WriteLine("Probe Summary:");
            Console.WriteLine($"  Successful probes: {successCount}/{results.Count}");
            Console.WriteLine($"  Failed probes: {results.Count - successCount}");
            Console.WriteLine($"  Fastest success: {DescribeFastestSuccess(results)}");
            Console.WriteLine($"  Fastest consensus responder: {DescribeFastestConsensusResponder(results)}");
            Console.WriteLine($"  Transport coverage: {DescribeTransportCoverage(results)}");
            Console.WriteLine($"  Answer consensus: {DescribeAnswerConsensus(results)}");
            Console.WriteLine($"  Mismatched responders: {DescribeMismatchedResponders(results)}");
            ProbePolicyOutcome policy = EvaluateProbePolicy(results, options);
            Console.WriteLine($"  Distinct answer sets: {DescribeDistinctAnswerSetCount(results)}");
            Console.WriteLine($"  Answer variants: {DescribeAnswerVariants(results)}");
            Console.WriteLine($"  Recommended endpoint: {DescribeRecommendedProbeResult(results, policy, options)}");
            Console.WriteLine($"  Policy result: {(policy.Passed ? "pass" : $"fail ({policy.Reason})")}");
            int exitCode = policy.ExitCode;
            if (options.ProbeSummaryLine) {
                Console.WriteLine(BuildProbeSummaryLine(results, policy, options, exitCode));
            }

            return exitCode;
        }

        private static async Task<int> RunCustomProbeAsync(CliOptions options, string domain, CancellationToken cancellationToken) {
            DnsResolverEndpoint[] plan = EndpointParser.TryParseMany(options.ProbeEndpoints, out IReadOnlyList<string> errors);
            if (errors.Count > 0) {
                foreach (string error in errors) {
                    Console.Error.WriteLine(error);
                }
                return 1;
            }

            Console.WriteLine("Probe:");
            Console.WriteLine($"  Domain: {domain}");
            Console.WriteLine("  Endpoint profile: custom");
            Console.WriteLine($"  Candidates: {plan.Length}");
            Console.WriteLine($"  Detail mode: {(options.ProbeSummaryOnly ? "summary-only" : "full")}");

            var results = new List<ProbeResult>(plan.Length);
            foreach (DnsResolverEndpoint endpoint in plan) {
                cancellationToken.ThrowIfCancellationRequested();
                ProbeResult result = await ProbeEndpointAsync(endpoint, domain, options, cancellationToken).ConfigureAwait(false);
                results.Add(result);
                if (!options.ProbeSummaryOnly) {
                    WriteProbeResult(result);
                }
            }

            int successCount = results.Count(result => result.Succeeded);
            Console.WriteLine("Probe Summary:");
            Console.WriteLine($"  Successful probes: {successCount}/{results.Count}");
            Console.WriteLine($"  Failed probes: {results.Count - successCount}");
            Console.WriteLine($"  Fastest success: {DescribeFastestSuccess(results)}");
            Console.WriteLine($"  Fastest consensus responder: {DescribeFastestConsensusResponder(results)}");
            Console.WriteLine($"  Transport coverage: {DescribeTransportCoverage(results)}");
            Console.WriteLine($"  Answer consensus: {DescribeAnswerConsensus(results)}");
            Console.WriteLine($"  Mismatched responders: {DescribeMismatchedResponders(results)}");
            ProbePolicyOutcome policy = EvaluateProbePolicy(results, options);
            Console.WriteLine($"  Distinct answer sets: {DescribeDistinctAnswerSetCount(results)}");
            Console.WriteLine($"  Answer variants: {DescribeAnswerVariants(results)}");
            Console.WriteLine($"  Recommended endpoint: {DescribeRecommendedProbeResult(results, policy, options)}");
            Console.WriteLine($"  Policy result: {(policy.Passed ? "pass" : $"fail ({policy.Reason})")}");
            int exitCode = policy.ExitCode;
            if (options.ProbeSummaryLine) {
                Console.WriteLine(BuildProbeSummaryLine(results, policy, options, exitCode));
            }

            return exitCode;
        }

        private static DnsEndpoint[] BuildProbePlan(DnsEndpoint endpoint) {
            return endpoint switch {
                DnsEndpoint.System or DnsEndpoint.SystemTcp => new[] {
                    DnsEndpoint.System,
                    DnsEndpoint.SystemTcp
                },
                DnsEndpoint.Cloudflare or
                DnsEndpoint.CloudflareWireFormat or
                DnsEndpoint.CloudflareWireFormatPost or
                DnsEndpoint.CloudflareJsonPost or
                DnsEndpoint.CloudflareQuic or
                DnsEndpoint.CloudflareOdoh => new[] {
                    DnsEndpoint.Cloudflare,
                    DnsEndpoint.CloudflareWireFormat,
                    DnsEndpoint.CloudflareWireFormatPost,
                    DnsEndpoint.CloudflareJsonPost,
                    DnsEndpoint.CloudflareQuic,
                    DnsEndpoint.CloudflareOdoh
                },
                DnsEndpoint.Google or
                DnsEndpoint.GoogleWireFormat or
                DnsEndpoint.GoogleWireFormatPost or
                DnsEndpoint.GoogleJsonPost or
                DnsEndpoint.GoogleQuic => new[] {
                    DnsEndpoint.Google,
                    DnsEndpoint.GoogleWireFormat,
                    DnsEndpoint.GoogleWireFormatPost,
                    DnsEndpoint.GoogleJsonPost,
                    DnsEndpoint.GoogleQuic
                },
                DnsEndpoint.AdGuard or
                DnsEndpoint.AdGuardFamily or
                DnsEndpoint.AdGuardNonFiltering => new[] {
                    DnsEndpoint.AdGuard,
                    DnsEndpoint.AdGuardFamily,
                    DnsEndpoint.AdGuardNonFiltering
                },
                DnsEndpoint.Quad9 or
                DnsEndpoint.Quad9ECS or
                DnsEndpoint.Quad9Unsecure => new[] {
                    DnsEndpoint.Quad9,
                    DnsEndpoint.Quad9ECS,
                    DnsEndpoint.Quad9Unsecure
                },
                DnsEndpoint.OpenDNS or
                DnsEndpoint.OpenDNSFamily => new[] {
                    DnsEndpoint.OpenDNS,
                    DnsEndpoint.OpenDNSFamily
                },
                DnsEndpoint.DnsCryptCloudflare or
                DnsEndpoint.DnsCryptQuad9 or
                DnsEndpoint.DnsCryptRelay => new[] {
                    DnsEndpoint.DnsCryptCloudflare,
                    DnsEndpoint.DnsCryptQuad9,
                    DnsEndpoint.DnsCryptRelay
                },
                _ => new[] { endpoint }
            };
        }

        private static async Task<ProbeResult> ProbeEndpointAsync(DnsEndpoint endpoint, string domain, CliOptions options, CancellationToken cancellationToken) {
            if (ProbeOverride != null) {
                var overrideResult = await ProbeOverride(endpoint, domain, cancellationToken).ConfigureAwait(false);
                return new ProbeResult {
                    Endpoint = endpoint,
                    DisplayName = endpoint.ToString(),
                    RequestFormat = overrideResult.RequestFormat,
                    Resolver = overrideResult.Resolver,
                    Response = overrideResult.Response,
                    Elapsed = overrideResult.Elapsed
                };
            }

            await using var client = new ClientX(endpoint);
            ConfigureClient(client, options);

            var stopwatch = Stopwatch.StartNew();
            try {
                DnsResponse response = await client.Resolve(
                    domain,
                    DnsRecordType.A,
                    options.RequestDnsSec,
                    options.ValidateDnsSec,
                    retryOnTransient: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                return new ProbeResult {
                    Endpoint = endpoint,
                    DisplayName = endpoint.ToString(),
                    RequestFormat = client.EndpointConfiguration.RequestFormat,
                    Resolver = DescribeResolver(client, response),
                    Response = response,
                    Elapsed = response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : stopwatch.Elapsed
                };
            } catch (Exception ex) {
                stopwatch.Stop();
                return new ProbeResult {
                    Endpoint = endpoint,
                    DisplayName = endpoint.ToString(),
                    RequestFormat = client.EndpointConfiguration.RequestFormat,
                    Resolver = DescribeConfiguredResolver(client),
                    Elapsed = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        }

        private static async Task<ProbeResult> ProbeEndpointAsync(DnsResolverEndpoint endpoint, string domain, CliOptions options, CancellationToken cancellationToken) {
            if (ProbeEndpointOverride != null) {
                var overrideResult = await ProbeEndpointOverride(endpoint, domain, cancellationToken).ConfigureAwait(false);
                return new ProbeResult {
                    Endpoint = DnsEndpoint.Custom,
                    DisplayName = DescribeProbeEndpoint(endpoint),
                    RequestFormat = overrideResult.RequestFormat,
                    Resolver = overrideResult.Resolver,
                    Response = overrideResult.Response,
                    Elapsed = overrideResult.Elapsed
                };
            }

            await using var client = CreateClient(endpoint);
            ConfigureClient(client, options);

            var stopwatch = Stopwatch.StartNew();
            try {
                client.EndpointConfiguration.TimeOut = Math.Max(1, client.EndpointConfiguration.TimeOut);
                client.EndpointConfiguration.UseTcpFallback = endpoint.AllowTcpFallback;
                if (endpoint.EdnsBufferSize.HasValue) {
                    client.EndpointConfiguration.UdpBufferSize = endpoint.EdnsBufferSize.Value;
                }
                if (endpoint.Timeout.HasValue) {
                    client.EndpointConfiguration.TimeOut = (int)Math.Max(1, endpoint.Timeout.Value.TotalMilliseconds);
                }
                if (endpoint.Transport != Transport.Doh) {
                    client.EndpointConfiguration.Port = endpoint.Port;
                }

                DnsResponse response = await client.Resolve(
                    domain,
                    options.RecordType,
                    options.RequestDnsSec || endpoint.DnsSecOk == true,
                    options.ValidateDnsSec,
                    retryOnTransient: false,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                return new ProbeResult {
                    Endpoint = DnsEndpoint.Custom,
                    DisplayName = DescribeProbeEndpoint(endpoint),
                    RequestFormat = client.EndpointConfiguration.RequestFormat,
                    Resolver = DescribeResolver(client, response),
                    Response = response,
                    Elapsed = response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : stopwatch.Elapsed
                };
            } catch (Exception ex) {
                stopwatch.Stop();
                return new ProbeResult {
                    Endpoint = DnsEndpoint.Custom,
                    DisplayName = DescribeProbeEndpoint(endpoint),
                    RequestFormat = MapTransport(endpoint.Transport),
                    Resolver = DescribeConfiguredResolver(client),
                    Elapsed = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        }

        private static void WriteProbeResult(ProbeResult result) {
            DnsResponse? response = result.Response;
            string target = result.DisplayName ?? result.Endpoint.ToString();
            string status = result.Succeeded ? "OK" : "FAIL";
            string responseStatus = response?.Status.ToString() ?? "NoResponse";
            string transport = response?.UsedTransport.ToString() ?? "(unknown)";
            int answers = response?.Answers?.Length ?? 0;
            string error = !string.IsNullOrWhiteSpace(result.Error)
                ? result.Error!
                : !string.IsNullOrWhiteSpace(response?.Error)
                    ? response!.Error
                    : response != null && response.ErrorCode != DnsQueryErrorCode.None
                        ? response.ErrorCode.ToString()
                        : "none";

            Console.WriteLine($"  [{status}] {target} via {result.RequestFormat}");
            Console.WriteLine($"      Resolver: {result.Resolver}");
            Console.WriteLine($"      Status: {responseStatus}");
            Console.WriteLine($"      Transport: {transport}");
            Console.WriteLine($"      Elapsed: {FormatDuration(result.Elapsed)}");
            Console.WriteLine($"      Answers: {answers}");
            if (!string.Equals(error, "none", StringComparison.OrdinalIgnoreCase)) {
                Console.WriteLine($"      Error: {error}");
            }
        }

        private static ClientX CreateClient(DnsResolverEndpoint endpoint) {
            if (endpoint.Transport == Transport.Doh) {
                Uri dohUri = endpoint.DohUrl ?? new Uri($"https://{endpoint.Host}/dns-query");
                return new ClientX(dohUri, MapTransport(endpoint.Transport));
            }

            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("Custom non-DoH probe endpoint requires Host.");
            }

            var client = new ClientX(endpoint.Host!, MapTransport(endpoint.Transport));
            client.EndpointConfiguration.Port = endpoint.Port;
            return client;
        }

        private static DnsRequestFormat MapTransport(Transport transport) {
            return transport switch {
                Transport.Udp => DnsRequestFormat.DnsOverUDP,
                Transport.Tcp => DnsRequestFormat.DnsOverTCP,
                Transport.Dot => DnsRequestFormat.DnsOverTLS,
                Transport.Doh => DnsRequestFormat.DnsOverHttps,
                Transport.Quic => DnsRequestFormat.DnsOverQuic,
                Transport.Grpc => DnsRequestFormat.DnsOverGrpc,
                Transport.Multicast => DnsRequestFormat.Multicast,
                _ => DnsRequestFormat.DnsOverUDP
            };
        }

        private static string DescribeProbeEndpoint(DnsResolverEndpoint endpoint) {
            string prefix = endpoint.Transport.ToString().ToLowerInvariant();
            return $"{prefix}@{endpoint}";
        }

        private static string DescribeFastestSuccess(IEnumerable<ProbeResult> results) {
            ProbeResult? fastest = GetFastestSuccessfulProbeResult(results);

            if (fastest == null) {
                return "none";
            }

            string target = fastest.DisplayName ?? fastest.Endpoint.ToString();
            string transport = DescribeProbeTransport(fastest);
            return $"{target} in {FormatDuration(fastest.Elapsed)} via {transport}";
        }

        private static string DescribeFastestConsensusResponder(IEnumerable<ProbeResult> results) {
            ProbeResult? fastest = GetFastestConsensusProbeResult(results);
            if (fastest == null) {
                return "none";
            }

            string target = fastest.DisplayName ?? fastest.Endpoint.ToString();
            string transport = DescribeProbeTransport(fastest);
            return $"{target} in {FormatDuration(fastest.Elapsed)} via {transport}";
        }

        private static string DescribeTransportCoverage(IEnumerable<ProbeResult> results) {
            string[] summary = results
                .GroupBy(DescribeProbeTransport, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Key} {group.Count(result => result.Succeeded)}/{group.Count()}")
                .ToArray();

            return summary.Length == 0 ? "none" : string.Join(" | ", summary);
        }

        private static string DescribeProbeTransport(ProbeResult result) {
            if (result.Response != null) {
                return result.Response.UsedTransport.ToString();
            }

            return MapRequestFormatToTransport(result.RequestFormat).ToString();
        }

        private static string DescribeAnswerConsensus(IEnumerable<ProbeResult> results) {
            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length == 0) {
                return "none";
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);

            return $"{groups[0].Length}/{successful.Length} successful probes agree";
        }

        private static string DescribeMismatchedResponders(IEnumerable<ProbeResult> results) {
            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length <= 1) {
                return "none";
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);

            if (groups.Length <= 1) {
                return "none";
            }

            string[] mismatches = groups
                .Skip(1)
                .SelectMany(static group => group)
                .Select(result => result.DisplayName ?? result.Endpoint.ToString())
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return mismatches.Length == 0 ? "none" : string.Join(", ", mismatches);
        }

        private static string DescribeDistinctAnswerSetCount(IEnumerable<ProbeResult> results) {
            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length == 0) {
                return "0";
            }

            return GetAnswerGroups(successful).Length.ToString(CultureInfo.InvariantCulture);
        }

        private static string DescribeAnswerVariants(IEnumerable<ProbeResult> results) {
            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length == 0) {
                return "none";
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);

            string[] variants = groups
                .Select((group, index) => $"[{index + 1}] {DescribeAnswerSet(group[0])} <- {DescribeProbeTargets(group)}")
                .ToArray();

            return variants.Length == 0 ? "none" : string.Join(" | ", variants);
        }

        private static ProbePolicyOutcome EvaluateProbePolicy(IEnumerable<ProbeResult> results, CliOptions options) {
            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            ProbeResult[] allResults = results.ToArray();
            if (successful.Length == 0) {
                return new ProbePolicyOutcome {
                    Passed = false,
                    Reason = "no successful probes",
                    ExitCode = 1
                };
            }

            if (options.ProbeMinSuccessCount.HasValue && successful.Length < options.ProbeMinSuccessCount.Value) {
                return new ProbePolicyOutcome {
                    Passed = false,
                    Reason = $"successful probes {successful.Length}/{allResults.Length} below required count {options.ProbeMinSuccessCount.Value}",
                    ExitCode = 3
                };
            }

            int successPercent = (int)Math.Round((double)successful.Length * 100 / allResults.Length, MidpointRounding.AwayFromZero);
            if (options.ProbeMinSuccessPercent.HasValue && successPercent < options.ProbeMinSuccessPercent.Value) {
                return new ProbePolicyOutcome {
                    Passed = false,
                    Reason = $"success rate {successPercent}% below required {options.ProbeMinSuccessPercent.Value}%",
                    ExitCode = 3
                };
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);
            int consensusPercent = (int)Math.Round((double)groups[0].Length * 100 / successful.Length, MidpointRounding.AwayFromZero);
            if (options.ProbeRequireConsensus && groups.Length > 1) {
                return new ProbePolicyOutcome {
                    Passed = false,
                    Reason = $"consensus required but top answer set reached {consensusPercent}%",
                    ExitCode = 2
                };
            }

            if (options.ProbeMinConsensusPercent.HasValue && consensusPercent < options.ProbeMinConsensusPercent.Value) {
                return new ProbePolicyOutcome {
                    Passed = false,
                    Reason = $"consensus {consensusPercent}% below required {options.ProbeMinConsensusPercent.Value}%",
                    ExitCode = 2
                };
            }

            return new ProbePolicyOutcome {
                Passed = true,
                ExitCode = 0
            };
        }

        private static string BuildProbeSummaryLine(IEnumerable<ProbeResult> results, ProbePolicyOutcome policy, CliOptions options, int exitCode) {
            ProbeResult[] allResults = results.ToArray();
            ProbeResult[] successful = GetSuccessfulProbeResults(allResults);
            ProbeResult[][] groups = GetAnswerGroups(successful);
            ProbeResult? fastestSuccess = GetFastestSuccessfulProbeResult(allResults);
            ProbeResult? fastestConsensus = GetFastestConsensusProbeResult(allResults);
            ProbeResult? recommended = GetRecommendedProbeResult(allResults, policy, options);
            string recommendationStatus = GetRecommendationStatus(policy, recommended);
            string recommendationSource = GetRecommendationSource(allResults, policy, options, recommended);
            string recommendationReason = GetRecommendationReason(allResults, policy, options, recommended);
            int successPercent = allResults.Length == 0
                ? 0
                : (int)Math.Round((double)successful.Length * 100 / allResults.Length, MidpointRounding.AwayFromZero);
            int consensusCount = groups.Length == 0 ? 0 : groups[0].Length;
            int consensusPercent = successful.Length == 0
                ? 0
                : (int)Math.Round((double)consensusCount * 100 / successful.Length, MidpointRounding.AwayFromZero);
            int distinctAnswerSets = groups.Length;

            return string.Join(" ", new[] {
                "PROBE_SUMMARY",
                $"result={(policy.Passed ? "pass" : "fail")}",
                $"exit_code={exitCode}",
                $"successful={successful.Length}",
                $"total={allResults.Length}",
                $"success_percent={successPercent}",
                $"consensus_count={consensusCount}",
                $"consensus_total={successful.Length}",
                $"consensus_percent={consensusPercent}",
                $"distinct_answer_sets={distinctAnswerSets}",
                $"fastest_success_target={NormalizeSummaryToken(GetProbeTarget(fastestSuccess))}",
                $"fastest_success_transport={NormalizeSummaryToken(GetProbeTransportName(fastestSuccess))}",
                $"fastest_success_ms={GetProbeElapsedMilliseconds(fastestSuccess)}",
                $"fastest_consensus_target={NormalizeSummaryToken(GetProbeTarget(fastestConsensus))}",
                $"fastest_consensus_transport={NormalizeSummaryToken(GetProbeTransportName(fastestConsensus))}",
                $"fastest_consensus_ms={GetProbeElapsedMilliseconds(fastestConsensus)}",
                $"recommended_target={NormalizeSummaryToken(GetProbeTarget(recommended))}",
                $"recommended_resolver={NormalizeSummaryToken(GetProbeResolver(recommended))}",
                $"recommended_transport={NormalizeSummaryToken(GetProbeTransportName(recommended))}",
                $"recommended_ms={GetProbeElapsedMilliseconds(recommended)}",
                $"recommended_status={NormalizeSummaryToken(recommendationStatus)}",
                $"recommendation_source={NormalizeSummaryToken(recommendationSource)}",
                $"why_not_recommended={NormalizeSummaryToken(recommendationReason)}",
                $"policy_reason={NormalizeSummaryToken(policy.Passed ? "none" : policy.Reason)}"
            });
        }

        private static ProbeResult[] GetSuccessfulProbeResults(IEnumerable<ProbeResult> results) {
            return results.Where(result => result.Succeeded).ToArray();
        }

        private static ProbeResult? GetFastestSuccessfulProbeResult(IEnumerable<ProbeResult> results) {
            return results
                .Where(result => result.Succeeded)
                .OrderBy(result => result.Elapsed)
                .FirstOrDefault();
        }

        private static ProbeResult? GetFastestConsensusProbeResult(IEnumerable<ProbeResult> results) {
            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length == 0) {
                return null;
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);
            return groups[0]
                .OrderBy(result => result.Elapsed)
                .FirstOrDefault();
        }

        private static ProbeResult? GetRecommendedProbeResult(IEnumerable<ProbeResult> results, ProbePolicyOutcome policy, CliOptions options) {
            if (!policy.Passed) {
                return null;
            }

            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length == 0) {
                return null;
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);
            if (groups.Length == 0) {
                return null;
            }

            if (groups.Length == 1) {
                return groups[0]
                    .OrderBy(result => result.Elapsed)
                    .FirstOrDefault();
            }

            if (groups[0].Length == groups[1].Length) {
                return null;
            }

            bool hasConsensusPolicy = options.ProbeRequireConsensus || options.ProbeMinConsensusPercent.HasValue;
            if (!hasConsensusPolicy) {
                return null;
            }

            return groups[0]
                .OrderBy(result => result.Elapsed)
                .FirstOrDefault();
        }

        private static string GetRecommendationReason(IEnumerable<ProbeResult> results, ProbePolicyOutcome policy, CliOptions options, ProbeResult? recommended) {
            if (recommended != null) {
                return "none";
            }

            if (!policy.Passed) {
                return "policy failed";
            }

            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length == 0) {
                return "no successful probes";
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);
            if (groups.Length == 0) {
                return "no answer groups";
            }

            if (groups.Length == 1) {
                return "none";
            }

            if (groups[0].Length == groups[1].Length) {
                return "top answer sets tied";
            }

            bool hasConsensusPolicy = options.ProbeRequireConsensus || options.ProbeMinConsensusPercent.HasValue;
            if (!hasConsensusPolicy) {
                return "consensus policy not enabled";
            }

            return "none";
        }

        private static string GetRecommendationSource(IEnumerable<ProbeResult> results, ProbePolicyOutcome policy, CliOptions options, ProbeResult? recommended) {
            if (recommended == null) {
                return "none";
            }

            ProbeResult[] successful = GetSuccessfulProbeResults(results);
            if (successful.Length <= 1) {
                return "single success fallback";
            }

            ProbeResult[][] groups = GetAnswerGroups(successful);
            if (groups.Length <= 1) {
                return "unanimous agreement";
            }

            bool hasConsensusPolicy = options.ProbeRequireConsensus || options.ProbeMinConsensusPercent.HasValue;
            if (hasConsensusPolicy) {
                return "consensus policy majority";
            }

            return "none";
        }

        private static string GetRecommendationStatus(ProbePolicyOutcome policy, ProbeResult? recommended) {
            if (recommended != null) {
                return "selected";
            }

            return policy.Passed ? "unavailable" : "blocked by policy";
        }

        private static ProbeResult[][] GetAnswerGroups(IEnumerable<ProbeResult> successfulResults) {
            return successfulResults
                .GroupBy(BuildAnswerSignature, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.ToArray())
                .ToArray();
        }

        private static string GetProbeTarget(ProbeResult? result) {
            if (result == null) {
                return "none";
            }

            return result.DisplayName ?? result.Endpoint.ToString();
        }

        private static string GetProbeResolver(ProbeResult? result) {
            if (result == null || string.IsNullOrWhiteSpace(result.Resolver)) {
                return "none";
            }

            return result.Resolver;
        }

        private static string GetProbeTransportName(ProbeResult? result) {
            if (result == null) {
                return "none";
            }

            return DescribeProbeTransport(result);
        }

        private static int GetProbeElapsedMilliseconds(ProbeResult? result) {
            if (result == null) {
                return 0;
            }

            return (int)Math.Round(result.Elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);
        }

        private static string DescribeRecommendedProbeResult(IEnumerable<ProbeResult> results, ProbePolicyOutcome policy, CliOptions options) {
            ProbeResult? recommended = GetRecommendedProbeResult(results, policy, options);
            if (recommended == null) {
                return "none";
            }

            string target = recommended.DisplayName ?? recommended.Endpoint.ToString();
            string transport = DescribeProbeTransport(recommended);
            return $"{target} in {FormatDuration(recommended.Elapsed)} via {transport}";
        }

        private static string NormalizeSummaryToken(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "none";
            }

            string text = value!;
            var chars = new char[text.Length];
            int length = 0;
            bool previousUnderscore = false;

            foreach (char c in text) {
                char normalized;
                if (char.IsLetterOrDigit(c)) {
                    normalized = char.ToLowerInvariant(c);
                    previousUnderscore = false;
                } else {
                    if (previousUnderscore) {
                        continue;
                    }
                    normalized = '_';
                    previousUnderscore = true;
                }

                chars[length++] = normalized;
            }

            if (length == 0) {
                return "none";
            }

            while (length > 0 && chars[length - 1] == '_') {
                length--;
            }

            return length == 0 ? "none" : new string(chars, 0, length);
        }

        private static string BuildAnswerSignature(ProbeResult result) {
            DnsResponse? response = result.Response;
            if (response?.Answers == null || response.Answers.Length == 0) {
                return "(no answers)";
            }

            string[] values = response.Answers
                .Select(answer => $"{answer.Name}|{answer.Type}|{answer.Data}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return string.Join(";", values);
        }

        private static string DescribeAnswerSet(ProbeResult result) {
            DnsResponse? response = result.Response;
            if (response?.Answers == null || response.Answers.Length == 0) {
                return "(no answers)";
            }

            string[] values = response.Answers
                .Select(answer => $"{answer.Name} {answer.Type} {answer.Data}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            return string.Join("; ", values);
        }

        private static string DescribeProbeTargets(IEnumerable<ProbeResult> results) {
            string[] targets = results
                .Select(result => result.DisplayName ?? result.Endpoint.ToString())
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return string.Join(", ", targets);
        }

        private static Transport MapRequestFormatToTransport(DnsRequestFormat requestFormat) {
            return requestFormat switch {
                DnsRequestFormat.DnsOverUDP => Transport.Udp,
                DnsRequestFormat.DnsOverTCP => Transport.Tcp,
                DnsRequestFormat.DnsOverTLS => Transport.Dot,
                DnsRequestFormat.DnsOverQuic => Transport.Quic,
                DnsRequestFormat.DnsOverGrpc => Transport.Grpc,
                DnsRequestFormat.Multicast => Transport.Multicast,
                DnsRequestFormat.DnsOverHttps => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsJSON => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsPOST => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsWirePost => Transport.Doh,
                DnsRequestFormat.DnsOverHttpsJSONPOST => Transport.Doh,
                DnsRequestFormat.DnsOverHttp2 => Transport.Doh,
                DnsRequestFormat.DnsOverHttp3 => Transport.Doh,
                DnsRequestFormat.ObliviousDnsOverHttps => Transport.Doh,
                _ => Transport.Udp
            };
        }

        private static void WriteExplain(
            ClientX client,
            DnsResponse response,
            TimeSpan elapsed,
            string operation,
            string target,
            DnsRecordType recordType,
            bool requestDnsSec,
            bool validateDnsSec,
            bool trace,
            string? zone = null,
            int? ttl = null) {
            Console.WriteLine("Explain:");
            Console.WriteLine($"  Operation: {operation}");
            if (!string.IsNullOrWhiteSpace(zone)) {
                Console.WriteLine($"  Zone: {zone}");
            }
            Console.WriteLine($"  Target: {target}");
            Console.WriteLine($"  Type: {recordType}");
            if (ttl.HasValue) {
                Console.WriteLine($"  TTL: {ttl.Value}");
            }
            Console.WriteLine($"  Resolver profile: {client.EndpointConfiguration.SelectionStrategy} via {client.EndpointConfiguration.RequestFormat}");
            Console.WriteLine($"  Resolver: {DescribeResolver(client, response)}");
            Console.WriteLine($"  Actual transport: {DescribeUsedTransport(response)}");
            Console.WriteLine($"  Cache enabled: {client.CacheEnabled}");
            Console.WriteLine($"  Attempts recorded: {client.AuditTrail.Count}");
            Console.WriteLine($"  Final source: {DescribeFinalSource(client)}");
            Console.WriteLine($"  Resolvers tried: {DescribeAttemptResolvers(client)}");
            Console.WriteLine($"  Retry reasons: {DescribeRetryReasons(client)}");
            Console.WriteLine($"  DNSSEC requested: {requestDnsSec}");
            Console.WriteLine($"  DNSSEC validated: {validateDnsSec}");
            Console.WriteLine($"  Retries: {response.RetryCount}");
            Console.WriteLine($"  Elapsed: {FormatDuration(response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : elapsed)}");
            Console.WriteLine($"  Answers: {response.Answers?.Length ?? 0}");
            Console.WriteLine($"  Authorities: {response.Authorities?.Length ?? 0}");
            Console.WriteLine($"  Additional: {response.Additional?.Length ?? 0}");
            Console.WriteLine($"  Truncated: {response.IsTruncated}");
            Console.WriteLine($"  AuthenticData: {response.AuthenticData}");
            Console.WriteLine($"  CheckingDisabled: {response.CheckingDisabled}");

            if (trace) {
                WriteTrace(client, response);
            }
        }

        private static void WriteTrace(ClientX client, DnsResponse response) {
            Console.WriteLine("Trace:");
            Console.WriteLine($"  Audit entries: {client.AuditTrail.Count}");
            Console.WriteLine($"  Question count: {response.Questions?.Length ?? 0}");
            Console.WriteLine($"  Used transport: {DescribeUsedTransport(response)}");
            Console.WriteLine($"  Error code: {response.ErrorCode}");
            if (!string.IsNullOrWhiteSpace(response.Error)) {
                Console.WriteLine($"  Error: {response.Error}");
            }
            if (!string.IsNullOrWhiteSpace(response.Comments)) {
                Console.WriteLine($"  Comments: {response.Comments}");
            }
            if (response.ExtendedDnsErrorInfo.Length > 0) {
                foreach (var ede in response.ExtendedDnsErrorInfo) {
                    Console.WriteLine($"  Extended DNS error: {ede.Code} {ede.Text}");
                }
            }
            foreach (var question in response.Questions ?? Array.Empty<DnsQuestion>()) {
                Console.WriteLine($"  Question: {question.Name} {question.Type} via {question.RequestFormat}");
            }
            foreach (var entry in client.AuditTrail) {
                int attempt = entry.AttemptNumber > 0 ? entry.AttemptNumber : 1;
                string outcome = entry.Response != null ? entry.Response.Status.ToString() : "NoResponse";
                string exception = entry.Exception?.GetType().Name ?? "None";
                string resolver = !string.IsNullOrWhiteSpace(entry.ResolverHost)
                    ? $"{entry.ResolverHost}:{entry.ResolverPort}"
                    : "(unknown)";
                string cache = entry.ServedFromCache ? "cache" : "network";
                string retry = string.IsNullOrWhiteSpace(entry.RetryReason) ? string.Empty : $", retry: {entry.RetryReason}";
                Console.WriteLine($"  Attempt {attempt}: {entry.Name} {entry.RecordType} via {entry.RequestFormat}/{entry.UsedTransport} to {resolver} => {outcome} in {FormatDuration(entry.Duration)} ({cache}, exception: {exception}{retry})");
            }
        }

        private static string DescribeResolver(ClientX client, DnsResponse response) {
            string? host = response.ServerAddress;
            if (string.IsNullOrWhiteSpace(host)) {
                host = client.EndpointConfiguration.BaseUri?.Host ?? client.EndpointConfiguration.Hostname;
            }

            if (string.IsNullOrWhiteSpace(host)) {
                host = "(unknown)";
            }

            int port = client.EndpointConfiguration.BaseUri?.Port ?? client.EndpointConfiguration.Port;
            return $"{host}:{port}";
        }

        private static string DescribeConfiguredResolver(ClientX client) {
            string host = client.EndpointConfiguration.BaseUri?.Host ?? client.EndpointConfiguration.Hostname ?? "(unknown)";
            int port = client.EndpointConfiguration.BaseUri?.Port ?? client.EndpointConfiguration.Port;
            return $"{host}:{port}";
        }

        private static string DescribeUsedTransport(DnsResponse response) {
            return response.UsedTransport.ToString();
        }

        private static string DescribeFinalSource(ClientX client) {
            AuditEntry? last = null;
            foreach (var entry in client.AuditTrail) {
                last = entry;
            }

            if (last == null) {
                return "unknown";
            }

            return last.ServedFromCache ? "cache" : "network";
        }

        private static string DescribeAttemptResolvers(ClientX client) {
            string[] resolvers = client.AuditTrail
                .Select(entry => !string.IsNullOrWhiteSpace(entry.ResolverHost)
                    ? $"{entry.ResolverHost}:{entry.ResolverPort}"
                    : "(unknown)")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return resolvers.Length == 0 ? "none" : string.Join(" -> ", resolvers);
        }

        private static string DescribeRetryReasons(ClientX client) {
            string[] reasons = client.AuditTrail
                .Select(entry => entry.RetryReason)
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Select(reason => reason!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return reasons.Length == 0 ? "none" : string.Join(" | ", reasons);
        }

        private static string FormatDuration(TimeSpan duration) {
            if (duration <= TimeSpan.Zero) {
                return "0 ms";
            }

            if (duration.TotalMilliseconds < 1000) {
                return $"{duration.TotalMilliseconds:F0} ms";
            }

            return $"{duration.TotalSeconds:F3} s";
        }

        private static void ShowHelp() {
            Console.WriteLine("DnsClientX.Cli - simple DNS query tool");
            Console.WriteLine("Usage: DnsClientX.Cli [options] <domain>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -t, --type <record>      DNS record type (default A)");
            Console.WriteLine("  -e, --endpoint <name>    DNS endpoint name (default System)");
            Console.WriteLine("      --dnssec             Request DNSSEC records");
            Console.WriteLine("      --validate-dnssec    Validate DNSSEC records");
            Console.WriteLine("      --wire-post          Use DNS over HTTPS wire POST (when supported)");
            Console.WriteLine("      --probe              Probe the selected endpoint profile and related variants");
            Console.WriteLine("      --probe-endpoint <endpoint>  Probe a custom endpoint such as tcp@1.1.1.1:53 or doh@https://dns.google/dns-query");
            Console.WriteLine("      --probe-summary-only  Suppress per-endpoint probe lines and print only the header and summary");
            Console.WriteLine("      --probe-summary-line  Append one stable PROBE_SUMMARY key=value line for automation");
            Console.WriteLine("      --probe-require-consensus    Fail when successful probe responders disagree");
            Console.WriteLine("      --probe-min-consensus <percent>  Fail when the top answer set is below the given 1-100 percentage");
            Console.WriteLine("      --probe-min-success <count>  Fail when fewer than the given number of probes succeed");
            Console.WriteLine("      --probe-min-success-percent <percent>  Fail when the probe success rate is below the given 1-100 percentage");
            Console.WriteLine("      --explain            Print resolver and response diagnostics");
            Console.WriteLine("      --trace              Print explain output plus audit details");
            Console.WriteLine("      --update <zone> <name> <type> <data>  Send dynamic update");
            Console.WriteLine("      --ttl <seconds>      TTL for update (default 300)");
            Console.WriteLine();
            Console.WriteLine("Available endpoints:");
            foreach (var (ep, desc) in DnsEndpointExtensions.GetAllWithDescriptions()) {
                Console.WriteLine($"  {ep,-20} {desc}");
            }
        }
    }
}
