using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static class Program {
        private enum QueryOutputFormat {
            Pretty,
            Json,
            Raw
        }

        private sealed class CliOptions {
            public string? Domain { get; set; }
            public DnsRecordType RecordType { get; set; } = DnsRecordType.A;
            public bool RecordTypeSpecified { get; set; }
            public DnsEndpoint Endpoint { get; set; } = DnsEndpoint.System;
            public QueryOutputFormat OutputFormat { get; set; } = QueryOutputFormat.Pretty;
            public bool ShortOutput { get; set; }
            public bool TxtConcatOutput { get; set; }
            public bool ShowQuestionSection { get; set; }
            public bool ShowAnswerSection { get; set; }
            public bool ShowAuthoritySection { get; set; }
            public bool ShowAdditionalSection { get; set; }
            public bool ReverseLookup { get; set; }
            public bool ZoneTransfer { get; set; }
            public bool TransferSummary { get; set; }
            public bool Benchmark { get; set; }
            public int BenchmarkAttempts { get; set; } = 3;
            public int BenchmarkTimeoutMs { get; set; } = 2000;
            public int BenchmarkConcurrency { get; set; } = 4;
            public int? BenchmarkMinSuccessPercent { get; set; }
            public int? BenchmarkMinSuccessfulCandidates { get; set; }
            public bool BenchmarkSummaryOnly { get; set; }
            public bool BenchmarkSummaryLine { get; set; }
            public List<DnsEndpoint> BenchmarkEndpoints { get; } = new List<DnsEndpoint>();
            public List<string> BenchmarkDomains { get; } = new List<string>();
            public List<DnsRecordType> BenchmarkRecordTypes { get; } = new List<DnsRecordType>();
            public List<string> EndpointInputs { get; } = new List<string>();
            public List<string> DomainInputs { get; } = new List<string>();
            public List<string> RecordTypeInputs { get; } = new List<string>();
            public List<string> ProbeEndpointFiles { get; } = new List<string>();
            public List<string> ProbeEndpointUrls { get; } = new List<string>();
            public bool RequestDnsSec { get; set; }
            public bool ValidateDnsSec { get; set; }
            public bool WirePost { get; set; }
            public bool Probe { get; set; }
            public List<string> ProbeEndpoints { get; } = new List<string>();
            public string? ProbeSavePath { get; set; }
            public string? ResolverSelectPath { get; set; }
            public string? ResolverUsePath { get; set; }
            public bool ProbeSummaryOnly { get; set; }
            public bool ProbeSummaryLine { get; set; }
            public bool ProbeRequireConsensus { get; set; }
            public int? ProbeMinConsensusPercent { get; set; }
            public int? ProbeMinSuccessCount { get; set; }
            public int? ProbeMinSuccessPercent { get; set; }
            public string? BenchmarkSavePath { get; set; }
            public bool Explain { get; set; }
            public bool Trace { get; set; }
            public bool ShowCapabilities { get; set; }
            public string? StampInfo { get; set; }
            public bool ResolverValidate { get; set; }
            public bool DoUpdate { get; set; }
            public string? Zone { get; set; }
            public string? UpdateName { get; set; }
            public string? UpdateData { get; set; }
            public int Ttl { get; set; } = 300;

            public bool HasExplicitSectionSelection =>
                ShowQuestionSection ||
                ShowAnswerSection ||
                ShowAuthoritySection ||
                ShowAdditionalSection;

            public bool HasCustomEndpointInputs =>
                ProbeEndpoints.Count > 0 ||
                ProbeEndpointFiles.Count > 0 ||
                ProbeEndpointUrls.Count > 0;
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
                CliOptions parsedOptions = options!;
                if (parsedOptions.Benchmark) {
                    return await RunBenchmarkAsync(parsedOptions, cts.Token).ConfigureAwait(false);
                }

                if (parsedOptions.ShowCapabilities) {
                    return WriteCapabilities(parsedOptions);
                }

                if (!string.IsNullOrWhiteSpace(parsedOptions.StampInfo)) {
                    return WriteStampInfo(parsedOptions);
                }

                if (parsedOptions.ResolverValidate) {
                    return await RunResolverValidationAsync(parsedOptions, cts.Token).ConfigureAwait(false);
                }

                if (parsedOptions.Probe) {
                    return await RunProbeAsync(parsedOptions, cts.Token).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(parsedOptions.ResolverSelectPath)) {
                    return await RunResolverSelectionAsync(parsedOptions, cts.Token).ConfigureAwait(false);
                }

                if (parsedOptions.ZoneTransfer) {
                    return await RunZoneTransferAsync(parsedOptions, cts.Token).ConfigureAwait(false);
                }

                return await RunStandardQueryAsync(parsedOptions, cts.Token).ConfigureAwait(false);
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
                        if (!TryReadNext(args, ref i, "--type", out string? recordTypeValue, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        AddOptionValues(options.RecordTypeInputs, recordTypeValue);
                        options.RecordTypeSpecified = true;
                        break;
                    case var opt when opt.Equals("-e", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--endpoint", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--endpoint", out string? endpointValue, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        AddOptionValues(options.EndpointInputs, endpointValue);
                        break;
                    case var opt when opt.Equals("--domain", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--domain", out string? domainValue, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        AddOptionValues(options.DomainInputs, domainValue);
                        break;
                    case var opt when opt.Equals("--reverse", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--ptr", StringComparison.OrdinalIgnoreCase):
                        options.ReverseLookup = true;
                        break;
                    case var opt when opt.Equals("--axfr", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--zone-transfer", StringComparison.OrdinalIgnoreCase):
                        options.ZoneTransfer = true;
                        break;
                    case var opt when opt.Equals("--transfer-summary", StringComparison.OrdinalIgnoreCase):
                        options.TransferSummary = true;
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
                    case var opt when opt.Equals("--format", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--format", out string? formatValue, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (!TryParseQueryOutputFormat(formatValue, out QueryOutputFormat outputFormat)) {
                            errorMessage = $"Invalid value for --format: {formatValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.OutputFormat = outputFormat;
                        break;
                    case var opt when opt.Equals("--short", StringComparison.OrdinalIgnoreCase):
                        options.ShortOutput = true;
                        break;
                    case var opt when opt.Equals("--txt-concat", StringComparison.OrdinalIgnoreCase):
                        options.TxtConcatOutput = true;
                        break;
                    case var opt when opt.Equals("--question", StringComparison.OrdinalIgnoreCase):
                        options.ShowQuestionSection = true;
                        break;
                    case var opt when opt.Equals("--answer", StringComparison.OrdinalIgnoreCase):
                        options.ShowAnswerSection = true;
                        break;
                    case var opt when opt.Equals("--authority", StringComparison.OrdinalIgnoreCase):
                        options.ShowAuthoritySection = true;
                        break;
                    case var opt when opt.Equals("--additional", StringComparison.OrdinalIgnoreCase):
                        options.ShowAdditionalSection = true;
                        break;
                    case var opt when opt.Equals("--benchmark", StringComparison.OrdinalIgnoreCase):
                        options.Benchmark = true;
                        break;
                    case var opt when opt.Equals("--benchmark-attempts", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-attempts", out string? benchmarkAttemptsValue, out errorMessage) ||
                            !int.TryParse(benchmarkAttemptsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int benchmarkAttempts)) {
                            errorMessage ??= $"Invalid value for --benchmark-attempts: {benchmarkAttemptsValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (benchmarkAttempts < 1) {
                            errorMessage = $"--benchmark-attempts must be at least 1: {benchmarkAttempts}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkAttempts = benchmarkAttempts;
                        break;
                    case var opt when opt.Equals("--benchmark-timeout", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-timeout", out string? benchmarkTimeoutValue, out errorMessage) ||
                            !int.TryParse(benchmarkTimeoutValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int benchmarkTimeoutMs)) {
                            errorMessage ??= $"Invalid value for --benchmark-timeout: {benchmarkTimeoutValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (benchmarkTimeoutMs < 1) {
                            errorMessage = $"--benchmark-timeout must be at least 1: {benchmarkTimeoutMs}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkTimeoutMs = benchmarkTimeoutMs;
                        break;
                    case var opt when opt.Equals("--benchmark-concurrency", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-concurrency", out string? benchmarkConcurrencyValue, out errorMessage) ||
                            !int.TryParse(benchmarkConcurrencyValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int benchmarkConcurrency)) {
                            errorMessage ??= $"Invalid value for --benchmark-concurrency: {benchmarkConcurrencyValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (benchmarkConcurrency < 1) {
                            errorMessage = $"--benchmark-concurrency must be at least 1: {benchmarkConcurrency}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkConcurrency = benchmarkConcurrency;
                        break;
                    case var opt when opt.Equals("--benchmark-min-success-percent", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-min-success-percent", out string? benchmarkMinSuccessPercentValue, out errorMessage) ||
                            !int.TryParse(benchmarkMinSuccessPercentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int benchmarkMinSuccessPercent)) {
                            errorMessage ??= $"Invalid value for --benchmark-min-success-percent: {benchmarkMinSuccessPercentValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (benchmarkMinSuccessPercent < 1 || benchmarkMinSuccessPercent > 100) {
                            errorMessage = $"--benchmark-min-success-percent must be between 1 and 100: {benchmarkMinSuccessPercent}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkMinSuccessPercent = benchmarkMinSuccessPercent;
                        break;
                    case var opt when opt.Equals("--benchmark-min-successful-candidates", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-min-successful-candidates", out string? benchmarkMinSuccessfulCandidatesValue, out errorMessage) ||
                            !int.TryParse(benchmarkMinSuccessfulCandidatesValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int benchmarkMinSuccessfulCandidates)) {
                            errorMessage ??= $"Invalid value for --benchmark-min-successful-candidates: {benchmarkMinSuccessfulCandidatesValue}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (benchmarkMinSuccessfulCandidates < 1) {
                            errorMessage = $"--benchmark-min-successful-candidates must be at least 1: {benchmarkMinSuccessfulCandidates}";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkMinSuccessfulCandidates = benchmarkMinSuccessfulCandidates;
                        break;
                    case var opt when opt.Equals("--benchmark-summary-line", StringComparison.OrdinalIgnoreCase):
                        options.Benchmark = true;
                        options.BenchmarkSummaryLine = true;
                        break;
                    case var opt when opt.Equals("--benchmark-save", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-save", out string? benchmarkSavePath, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkSavePath = benchmarkSavePath;
                        break;
                    case var opt when opt.Equals("--benchmark-summary-only", StringComparison.OrdinalIgnoreCase):
                        options.Benchmark = true;
                        options.BenchmarkSummaryOnly = true;
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
                        AddOptionValues(options.ProbeEndpoints, probeEndpoint);
                        break;
                    case var opt when opt.Equals("--resolver-file", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--resolver-file", out string? resolverFile, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        AddOptionValues(options.ProbeEndpointFiles, resolverFile);
                        break;
                    case var opt when opt.Equals("--resolver-url", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--resolver-url", out string? resolverUrl, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        AddOptionValues(options.ProbeEndpointUrls, resolverUrl);
                        break;
                    case var opt when opt.Equals("--resolver-validate", StringComparison.OrdinalIgnoreCase):
                        options.ResolverValidate = true;
                        break;
                    case var opt when opt.Equals("--probe-summary-only", StringComparison.OrdinalIgnoreCase):
                        options.Probe = true;
                        options.ProbeSummaryOnly = true;
                        break;
                    case var opt when opt.Equals("--probe-summary-line", StringComparison.OrdinalIgnoreCase):
                        options.Probe = true;
                        options.ProbeSummaryLine = true;
                        break;
                    case var opt when opt.Equals("--resolver-select", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--resolver-select", out string? resolverSelectPath, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.ResolverSelectPath = resolverSelectPath;
                        break;
                    case var opt when opt.Equals("--resolver-use", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--resolver-use", out string? resolverUsePath, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.ResolverUsePath = resolverUsePath;
                        break;
                    case var opt when opt.Equals("--probe-save", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--probe-save", out string? probeSavePath, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Probe = true;
                        options.ProbeSavePath = probeSavePath;
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
                    case var opt when opt.Equals("--capabilities", StringComparison.OrdinalIgnoreCase):
                        options.ShowCapabilities = true;
                        break;
                    case var opt when opt.Equals("--stamp-info", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--stamp-info", out string? stampInfo, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.StampInfo = stampInfo;
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

            if (!FinalizeParsedLists(options, out errorMessage)) {
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.HasCustomEndpointInputs && !options.Benchmark && !options.ResolverValidate) {
                options.Probe = true;
            }

            if (options.ShowCapabilities &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || !string.IsNullOrWhiteSpace(options.ResolverUsePath) ||
                 !string.IsNullOrWhiteSpace(options.StampInfo) || options.ResolverValidate)) {
                errorMessage = "--capabilities cannot be combined with query, probe, benchmark, update, axfr, resolver selection, or stamp modes.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.StampInfo) &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || !string.IsNullOrWhiteSpace(options.ResolverUsePath) ||
                 options.ResolverValidate)) {
                errorMessage = "--stamp-info cannot be combined with query, probe, benchmark, update, axfr, or resolver selection modes.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ResolverValidate &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || !string.IsNullOrWhiteSpace(options.ResolverUsePath))) {
                errorMessage = "--resolver-validate cannot be combined with query, probe, benchmark, update, axfr, or resolver selection modes.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.DoUpdate && (options.Probe || options.Benchmark || options.ZoneTransfer)) {
                errorMessage = options.Benchmark
                    ? "--benchmark cannot be combined with --update."
                    : options.Probe
                        ? "--probe cannot be combined with --update."
                        : "--axfr cannot be combined with --update.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.Probe && options.Benchmark) {
                errorMessage = "--probe cannot be combined with --benchmark.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer)) {
                errorMessage = "--resolver-select cannot be combined with probe, benchmark, update, or axfr modes.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath) &&
                (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) || options.DoUpdate || options.ZoneTransfer)) {
                errorMessage = "--resolver-use cannot be combined with --resolver-select, update, or axfr modes.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath) &&
                (options.EndpointInputs.Count > 0 || options.HasCustomEndpointInputs)) {
                errorMessage = "--resolver-use cannot be combined with --endpoint, --probe-endpoint, --resolver-file, or --resolver-url.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ZoneTransfer && (options.Probe || options.Benchmark)) {
                errorMessage = options.Probe
                    ? "--axfr cannot be combined with --probe."
                    : "--axfr cannot be combined with --benchmark.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ZoneTransfer && options.ReverseLookup) {
                errorMessage = "--reverse cannot be combined with --axfr.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!ApplyReverseLookupDefaults(options, out errorMessage)) {
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ShortOutput && options.OutputFormat != QueryOutputFormat.Pretty) {
                errorMessage = "--short cannot be combined with --format json or --format raw.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.TxtConcatOutput && options.OutputFormat == QueryOutputFormat.Json) {
                errorMessage = "--txt-concat cannot be combined with --format json.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ShowCapabilities &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw)) {
                errorMessage = "Capability mode supports only default output or --format json.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.StampInfo) &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw ||
                 options.ReverseLookup || options.RecordTypeSpecified || options.RequestDnsSec || options.ValidateDnsSec || options.WirePost ||
                 !string.IsNullOrWhiteSpace(options.Domain))) {
                errorMessage = "Stamp mode supports only --stamp-info and optional --format json.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ResolverValidate &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw ||
                 options.ReverseLookup || options.RecordTypeSpecified || options.RequestDnsSec || options.ValidateDnsSec || options.WirePost ||
                 !string.IsNullOrWhiteSpace(options.Domain) || !options.HasCustomEndpointInputs)) {
                errorMessage = "Resolver validation mode requires --resolver-validate with --probe-endpoint, --resolver-file, or --resolver-url, and supports optional --format json.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if ((options.Benchmark || options.Probe || options.DoUpdate) &&
                (options.ShortOutput || options.TxtConcatOutput || options.OutputFormat != QueryOutputFormat.Pretty || options.HasExplicitSectionSelection || options.TransferSummary)) {
                errorMessage = "Query output switches (--format, --short, --txt-concat, --question, --answer, --authority, --additional) apply only to standard query mode.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw)) {
                errorMessage = "Resolver selection mode supports only default output or --format json.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath) &&
                (options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw && (options.Probe || options.Benchmark))) {
                errorMessage = "Resolver reuse mode does not change the output rules of probe or benchmark workflows.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) &&
                (options.ReverseLookup || options.RecordTypeSpecified || options.RequestDnsSec || options.ValidateDnsSec || options.WirePost)) {
                errorMessage = "Query switches do not apply to --resolver-select.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ZoneTransfer &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.OutputFormat == QueryOutputFormat.Raw)) {
                errorMessage = "AXFR mode supports only --format pretty|json and optional --transfer-summary.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.TransferSummary && !options.ZoneTransfer) {
                errorMessage = "--transfer-summary applies only to --axfr mode.";
                invalidSwitches = null;
                options = null;
                return false;
            }

            if (options.ZoneTransfer && options.RecordTypeSpecified && options.RecordType != DnsRecordType.AXFR) {
                errorMessage = "--type can be used with --axfr only when set to AXFR.";
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
            } else if (options.ShowCapabilities) {
                // Capability mode does not require a domain.
            } else if (!string.IsNullOrWhiteSpace(options.StampInfo)) {
                // Stamp mode requires only the stamp input.
            } else if (options.ResolverValidate) {
                // Resolver validation mode requires only resolver inputs.
            } else if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath)) {
                // Selection mode requires only the saved snapshot path.
            } else if (options.ZoneTransfer && string.IsNullOrWhiteSpace(options.Domain)) {
                errorMessage = "Zone name is required.";
                invalidSwitches = null;
                options = null;
                return false;
            } else if (!options.Probe && !options.Benchmark && string.IsNullOrWhiteSpace(options.Domain)) {
                errorMessage = "Domain name is required.";
                invalidSwitches = null;
                options = null;
                return false;
            } else if (options.Probe && string.IsNullOrWhiteSpace(options.Domain)) {
                options.Domain = "example.com";
            } else if (options.Benchmark) {
                if (options.BenchmarkDomains.Count == 0) {
                    options.BenchmarkDomains.Add(!string.IsNullOrWhiteSpace(options.Domain) ? options.Domain! : "example.com");
                }
                if (options.BenchmarkEndpoints.Count == 0 && !options.HasCustomEndpointInputs) {
                    options.BenchmarkEndpoints.Add(options.Endpoint);
                }
                if (options.BenchmarkRecordTypes.Count == 0) {
                    options.BenchmarkRecordTypes.Add(options.RecordType);
                }
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

        private static bool TryParseQueryOutputFormat(string? value, out QueryOutputFormat format) {
            switch (value?.Trim().ToLowerInvariant()) {
                case "pretty":
                    format = QueryOutputFormat.Pretty;
                    return true;
                case "json":
                    format = QueryOutputFormat.Json;
                    return true;
                case "raw":
                    format = QueryOutputFormat.Raw;
                    return true;
                default:
                    format = default;
                    return false;
            }
        }

        private static bool ApplyReverseLookupDefaults(CliOptions options, out string? errorMessage) {
            errorMessage = null;

            if (options.ReverseLookup) {
                if (options.RecordTypeSpecified &&
                    options.BenchmarkRecordTypes.Any(recordType => recordType != DnsRecordType.PTR)) {
                    errorMessage = "--reverse cannot be combined with a record type other than PTR.";
                    return false;
                }

                options.RecordType = DnsRecordType.PTR;
                if (options.BenchmarkRecordTypes.Count > 0) {
                    options.BenchmarkRecordTypes.Clear();
                    options.BenchmarkRecordTypes.Add(DnsRecordType.PTR);
                }

                return true;
            }

            if (options.RecordTypeSpecified || options.DoUpdate) {
                return true;
            }

            string[] effectiveBenchmarkDomains = options.Benchmark ? GetEffectiveBenchmarkDomains(options) : Array.Empty<string>();
            bool shouldUsePtrByDefault = options.Benchmark
                ? effectiveBenchmarkDomains.Length > 0 && effectiveBenchmarkDomains.All(IsIpAddressLiteral)
                : !string.IsNullOrWhiteSpace(options.Domain) && IsIpAddressLiteral(options.Domain);

            if (shouldUsePtrByDefault) {
                options.RecordType = DnsRecordType.PTR;
            }

            return true;
        }

        private static bool IsIpAddressLiteral(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return false;
            }

            string trimmed = value!.Trim();
            return IPAddress.TryParse(trimmed, out _);
        }

        private static void AddOptionValues(ICollection<string> target, string? rawValue) {
            foreach (string value in SplitOptionValues(rawValue)) {
                target.Add(value);
            }
        }

        private static IEnumerable<string> SplitOptionValues(string? rawValue) {
            if (string.IsNullOrWhiteSpace(rawValue)) {
                yield break;
            }

            string valueText = rawValue!;
            string[] parts = valueText.Split(',');
            foreach (string part in parts) {
                string value = part.Trim();
                if (!string.IsNullOrWhiteSpace(value)) {
                    yield return value;
                }
            }
        }

        private static bool FinalizeParsedLists(CliOptions options, out string? errorMessage) {
            errorMessage = null;

            if (options.EndpointInputs.Count > 0) {
                foreach (string endpointValue in options.EndpointInputs) {
                    if (!Enum.TryParse(endpointValue, true, out DnsEndpoint endpoint)) {
                        errorMessage = $"Invalid value for --endpoint: {endpointValue}";
                        return false;
                    }
                    if (endpoint == DnsEndpoint.Custom) {
                        errorMessage = "--endpoint Custom is not supported in the CLI. Use explicit resolver endpoint syntax such as --probe-endpoint, --resolver-file, or --resolver-url instead.";
                        return false;
                    }
                    options.BenchmarkEndpoints.Add(endpoint);
                }

                options.Endpoint = options.BenchmarkEndpoints[0];
                if (!options.Benchmark && options.BenchmarkEndpoints.Count > 1) {
                    errorMessage = "--endpoint accepts multiple values only with --benchmark.";
                    return false;
                }
            }

            if (options.RecordTypeInputs.Count > 0) {
                foreach (string recordTypeValue in options.RecordTypeInputs) {
                    if (!Enum.TryParse(recordTypeValue, true, out DnsRecordType recordType)) {
                        errorMessage = $"Invalid value for --type: {recordTypeValue}";
                        return false;
                    }
                    options.BenchmarkRecordTypes.Add(recordType);
                }

                options.RecordType = options.BenchmarkRecordTypes[0];
                if (!options.Benchmark && options.BenchmarkRecordTypes.Count > 1) {
                    errorMessage = "--type accepts multiple values only with --benchmark.";
                    return false;
                }
            }

            if (options.DomainInputs.Count > 0) {
                foreach (string domainValue in options.DomainInputs) {
                    options.BenchmarkDomains.Add(domainValue);
                }

                options.Domain = options.BenchmarkDomains[0];
                if (!options.Benchmark && options.BenchmarkDomains.Count > 1) {
                    errorMessage = "--domain accepts multiple values only with --benchmark.";
                    return false;
                }
            }

            return true;
        }

        private static ResolverExecutionClientOptions CreateExecutionClientOptions(CliOptions options, bool useBenchmarkTimeout = false) {
            string? envPort = Environment.GetEnvironmentVariable("DNSCLIENTX_CLI_PORT");
            int? customPort = int.TryParse(envPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort) && parsedPort > 0
                ? parsedPort
                : null;

            return new ResolverExecutionClientOptions {
                EnableAudit = options.Explain || options.Trace,
                TimeoutMs = useBenchmarkTimeout ? options.BenchmarkTimeoutMs : null,
                PortOverride = customPort,
                ForceDohWirePost = options.WirePost
            };
        }

        private static async Task<int> RunZoneTransferAsync(CliOptions options, CancellationToken cancellationToken) {
            ResolverExecutionClientOptions clientOptions = CreateExecutionClientOptions(options);
            RecursiveZoneTransferResult result = await ResolverZoneTransferWorkflow.RunRecursiveAsync(
                new ResolverExecutionTargetSource {
                    BuiltInEndpoints = new[] { options.Endpoint }
                },
                options.Domain!,
                port: clientOptions.PortOverride ?? 53,
                clientOptions: clientOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            WriteZoneTransferResponse(result, options);
            return 0;
        }

        private static async Task<int> RunStandardQueryAsync(CliOptions options, CancellationToken cancellationToken) {
            ResolverExecutionTargetSource targetSource = CreateStandardQueryTargetSource(options);
            ResolverExecutionClientOptions clientOptions = CreateExecutionClientOptions(options);
            ResolverSingleOperationResult result;
            DnsResponse response;
            if (options.DoUpdate) {
                result = await ResolverSingleOperationWorkflow.UpdateAsync(
                    targetSource,
                    options.Zone!,
                    options.UpdateName!,
                    options.RecordType,
                    options.UpdateData!,
                    options.Ttl,
                    clientOptions,
                    cancellationToken).ConfigureAwait(false);
                response = result.Response;

                Console.WriteLine($"Update status: {response.Status} (retries {response.RetryCount})");
                if (options.Explain || options.Trace) {
                    WriteExplain(
                        result,
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
                result = await ResolverSingleOperationWorkflow.QueryAsync(
                    targetSource,
                    options.Domain!,
                    options.RecordType,
                    options.RequestDnsSec,
                    options.ValidateDnsSec,
                    clientOptions,
                    cancellationToken).ConfigureAwait(false);
                response = result.Response;

                WriteQueryResponse(response, options, result.Elapsed);

                if (options.Explain || options.Trace) {
                    WriteExplain(
                        result,
                        operation: "query",
                        target: options.Domain!,
                        recordType: options.RecordType,
                        requestDnsSec: options.RequestDnsSec,
                        validateDnsSec: options.ValidateDnsSec,
                        trace: options.Trace);
                }
            }

            return 0;
        }

        private static ResolverExecutionTargetSource CreateStandardQueryTargetSource(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return new ResolverExecutionTargetSource {
                    ResolverSelectionPath = options.ResolverUsePath
                };
            }

            return new ResolverExecutionTargetSource {
                BuiltInEndpoints = new[] { options.Endpoint }
            };
        }

        private static Task<int> RunResolverSelectionAsync(CliOptions options, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            ResolverSelectionResult selection = LoadRecommendedResolverSelection(options.ResolverSelectPath!);

            WriteResolverSelection(selection, options);
            return Task.FromResult(0);
        }

        private static ResolverSelectionResult LoadRecommendedResolverSelection(string path) {
            return ResolverExecutionTargetResolver.LoadRecommendedSelection(path);
        }

        private static int WriteCapabilities(CliOptions options) {
            DnsTransportCapabilityInfo[] capabilities = DnsTransportCapabilities.GetCapabilityReport();
            if (options.OutputFormat == QueryOutputFormat.Json) {
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(capabilities));
                return 0;
            }

            foreach (string line in DnsTransportCapabilityTextFormatter.BuildLines(capabilities)) {
                Console.WriteLine(line);
            }

            return 0;
        }

        private static int WriteStampInfo(CliOptions options) {
            DnsStampInfo info = DnsStamp.Describe(options.StampInfo!);
            if (options.OutputFormat == QueryOutputFormat.Json) {
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(info));
                return 0;
            }

            Console.WriteLine("DNS Stamp:");
            Console.WriteLine($"  Transport: {info.Transport}");
            Console.WriteLine($"  Request format: {info.RequestFormat}");
            Console.WriteLine($"  Host: {info.Host}");
            Console.WriteLine($"  Port: {info.Port}");
            if (info.DohUrl != null) {
                Console.WriteLine($"  DoH URL: {info.DohUrl}");
            }
            Console.WriteLine($"  DNSSEC property: {(info.DnsSecOk ? "yes" : "no")}");
            Console.WriteLine($"  Endpoint: {info.Endpoint}");
            Console.WriteLine($"  Normalized stamp: {info.NormalizedStamp}");
            return 0;
        }

        private static async Task<int> RunResolverValidationAsync(CliOptions options, CancellationToken cancellationToken) {
            ResolverEndpointValidationResult[] results = await EndpointParser.ValidateManyAsync(
                options.ProbeEndpoints,
                options.ProbeEndpointFiles,
                options.ProbeEndpointUrls,
                cancellationToken).ConfigureAwait(false);

            if (options.OutputFormat == QueryOutputFormat.Json) {
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(results));
            } else {
                WriteResolverValidationResults(results);
            }

            return results.Any(result => !result.IsValid) ? 1 : 0;
        }

        private static void WriteResolverValidationResults(ResolverEndpointValidationResult[] results) {
            int valid = results.Count(result => result.IsValid);
            int invalid = results.Length - valid;

            Console.WriteLine("Resolver Validation:");
            Console.WriteLine($"  Entries: {results.Length}");
            Console.WriteLine($"  Valid: {valid}");
            Console.WriteLine($"  Invalid: {invalid}");

            foreach (ResolverEndpointValidationResult result in results) {
                string location = string.IsNullOrWhiteSpace(result.Source)
                    ? "unknown"
                    : result.LineNumber.HasValue
                        ? $"{result.Source}:{result.LineNumber.Value}"
                        : result.Source;

                if (result.IsValid) {
                    Console.WriteLine($"  valid   {location}  {result.Entry} -> {result.Endpoint}");
                } else {
                    Console.WriteLine($"  invalid {location}  {result.Entry} ({result.Error})");
                }
            }
        }

        private static async Task<int> RunBenchmarkAsync(CliOptions options, CancellationToken cancellationToken) {
            string[] domains = options.BenchmarkDomains
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            DnsRecordType[] recordTypes = options.BenchmarkRecordTypes
                .Distinct()
                .ToArray();
            ResolverExecutionTargetSource targetSource = CreateBenchmarkTargetSource(options);
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, cancellationToken).ConfigureAwait(false);
            if (options.HasCustomEndpointInputs && targets.Length == 0) {
                Console.Error.WriteLine("No custom resolver endpoints were supplied.");
                return 1;
            }

            return await RunBenchmarkWithTargetsAsync(
                options,
                targets,
                domains,
                recordTypes,
                GetBenchmarkEndpointProfile(options),
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> RunBenchmarkWithTargetsAsync(
            CliOptions options,
            ResolverExecutionTarget[] targets,
            string[] domains,
            DnsRecordType[] recordTypes,
            string? endpointProfile,
            CancellationToken cancellationToken) {
            Console.WriteLine("Benchmark:");
            Console.WriteLine($"  Domains: {string.Join(", ", domains)}");
            Console.WriteLine($"  Types: {string.Join(", ", recordTypes)}");
            Console.WriteLine($"  Attempts per combination: {options.BenchmarkAttempts}");
            Console.WriteLine($"  Timeout (ms): {options.BenchmarkTimeoutMs}");
            Console.WriteLine($"  Concurrency: {options.BenchmarkConcurrency}");
            Console.WriteLine($"  Detail mode: {(options.BenchmarkSummaryOnly ? "summary-only" : "full")}");
            if (!string.IsNullOrWhiteSpace(endpointProfile)) {
                Console.WriteLine($"  Endpoint profile: {endpointProfile}");
            }

            Console.WriteLine($"  Candidates: {targets.Length}");
            Console.WriteLine($"  Queries per candidate: {domains.Length * recordTypes.Length * options.BenchmarkAttempts}");

            ResolverBenchmarkReport report = await ExecuteBenchmarkReportAsync(
                targets,
                domains,
                recordTypes,
                options,
                cancellationToken).ConfigureAwait(false);
            if (!options.BenchmarkSummaryOnly) {
                foreach (ResolverBenchmarkReportResult result in report.Results) {
                    WriteBenchmarkResult(result);
                }
            }

            return WriteBenchmarkSummary(report, options);
        }

        private static void WriteBenchmarkResult(ResolverBenchmarkReportResult result) {
            foreach (string line in ResolverReportTextFormatter.BuildBenchmarkResultLines(result, FormatDuration)) {
                Console.WriteLine(line);
            }
        }

        private static int WriteBenchmarkSummary(ResolverBenchmarkReport report, CliOptions options) {
            ResolverBenchmarkEvaluation evaluation = report.Evaluation;

            Console.WriteLine("Benchmark Summary:");
            Console.WriteLine($"  Successful candidates: {report.Summary.SuccessfulCandidates}/{report.Summary.CandidateCount}");
            Console.WriteLine($"  Successful queries: {report.Summary.OverallSuccessCount}/{report.Summary.OverallQueryCount}");
            Console.WriteLine($"  Successful query rate: {report.Summary.OverallSuccessPercent}%");
            Console.WriteLine($"  Runtime capability hints: {ResolverReportTextFormatter.DescribeBenchmarkRuntimeCapabilities(report.Summary)}");
            for (int i = 0; i < report.Results.Length; i++) {
                Console.WriteLine(ResolverReportTextFormatter.BuildBenchmarkRankedLine(report.Results[i], i + 1, FormatDuration));
            }
            Console.WriteLine($"  Best endpoint: {ResolverReportTextFormatter.DescribeBenchmarkBest(report.Results, report.Summary, FormatDuration)}");
            Console.WriteLine($"  Policy result: {(evaluation.PolicyPassed ? "pass" : $"fail ({evaluation.PolicyReason})")}");

            int exitCode = GetBenchmarkExitCode(evaluation, options);
            if (options.BenchmarkSummaryLine) {
                Console.WriteLine(ResolverReportTextFormatter.BuildBenchmarkSummaryLine(report.Summary, exitCode));
            }

            if (!string.IsNullOrWhiteSpace(options.BenchmarkSavePath)) {
                SaveResolverScoreSnapshot(options.BenchmarkSavePath!, report.Snapshot);
            }

            return exitCode;
        }

        private static ResolverBenchmarkPolicy CreateBenchmarkPolicy(CliOptions options) {
            return new ResolverBenchmarkPolicy {
                MinSuccessPercent = options.BenchmarkMinSuccessPercent,
                MinSuccessfulCandidates = options.BenchmarkMinSuccessfulCandidates
            };
        }

        private static int GetBenchmarkExitCode(ResolverBenchmarkEvaluation evaluation, CliOptions options) {
            if (evaluation.SuccessfulCandidates == 0) {
                return 1;
            }

            if (options.BenchmarkMinSuccessfulCandidates.HasValue && evaluation.SuccessfulCandidates < options.BenchmarkMinSuccessfulCandidates.Value) {
                return 3;
            }

            if (options.BenchmarkMinSuccessPercent.HasValue && evaluation.OverallSuccessPercent < options.BenchmarkMinSuccessPercent.Value) {
                return 3;
            }

            return evaluation.PolicyPassed ? 0 : 1;
        }

        private static async Task<int> RunProbeAsync(CliOptions options, CancellationToken cancellationToken) {
            string domain = options.Domain!;
            ResolverExecutionTargetSource targetSource = CreateProbeTargetSource(options);
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(targetSource, cancellationToken).ConfigureAwait(false);
            if (options.HasCustomEndpointInputs && targets.Length == 0) {
                Console.Error.WriteLine("No custom resolver endpoints were supplied.");
                return 1;
            }

            return await RunProbeWithTargetsAsync(
                options,
                targets,
                domain,
                GetProbeEndpointProfile(options),
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<int> RunProbeWithTargetsAsync(
            CliOptions options,
            ResolverExecutionTarget[] targets,
            string domain,
            string endpointProfile,
            CancellationToken cancellationToken) {
            Console.WriteLine("Probe:");
            Console.WriteLine($"  Domain: {domain}");
            Console.WriteLine($"  Endpoint profile: {endpointProfile}");
            Console.WriteLine($"  Candidates: {targets.Length}");
            Console.WriteLine($"  Detail mode: {(options.ProbeSummaryOnly ? "summary-only" : "full")}");

            ResolverProbeReport report = await ExecuteProbeReportAsync(targets, domain, options, cancellationToken).ConfigureAwait(false);
            if (!options.ProbeSummaryOnly) {
                foreach (ResolverProbeReportResult result in report.Results) {
                    WriteProbeResult(result);
                }
            }

            return WriteProbeSummary(report, options);
        }

        private static ResolverQueryRunOptions CreateQueryRunOptions(CliOptions options) {
            ResolverExecutionClientOptions clientOptions = CreateExecutionClientOptions(options, useBenchmarkTimeout: options.Benchmark);
            return new ResolverQueryRunOptions {
                TimeoutMs = options.Benchmark ? options.BenchmarkTimeoutMs : Configuration.DefaultTimeout,
                RequestDnsSec = options.RequestDnsSec,
                ValidateDnsSec = options.ValidateDnsSec,
                MaxRetries = 1,
                RetryDelayMs = 0,
                PortOverride = clientOptions.PortOverride,
                ForceDohWirePost = clientOptions.ForceDohWirePost
            };
        }

        private static string[] GetEffectiveBenchmarkDomains(CliOptions options) {
            if (options.BenchmarkDomains.Count > 0) {
                return options.BenchmarkDomains.ToArray();
            }

            return string.IsNullOrWhiteSpace(options.Domain)
                ? Array.Empty<string>()
                : new[] { options.Domain! };
        }

        private static Task<ResolverBenchmarkReport> ExecuteBenchmarkReportAsync(
            ResolverExecutionTarget[] targets,
            string[] domains,
            DnsRecordType[] recordTypes,
            CliOptions options,
            CancellationToken cancellationToken) {
            return ResolverBenchmarkWorkflow.RunAsync(
                targets,
                domains,
                recordTypes,
                options.BenchmarkAttempts,
                options.BenchmarkConcurrency,
                options.BenchmarkTimeoutMs,
                CreateQueryRunOptions(options),
                CreateBenchmarkPolicy(options),
                builtInOverride: CreateBuiltInOverride(),
                explicitOverride: CreateExplicitOverride(),
                cancellationToken: cancellationToken);
        }

        private static ResolverExecutionTargetSource CreateBenchmarkTargetSource(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return new ResolverExecutionTargetSource {
                    ResolverSelectionPath = options.ResolverUsePath
                };
            }

            if (options.HasCustomEndpointInputs) {
                return new ResolverExecutionTargetSource {
                    ResolverEndpoints = options.ProbeEndpoints.ToArray(),
                    ResolverEndpointFiles = options.ProbeEndpointFiles.ToArray(),
                    ResolverEndpointUrls = options.ProbeEndpointUrls.ToArray()
                };
            }

            return new ResolverExecutionTargetSource {
                BuiltInEndpoints = options.BenchmarkEndpoints.Distinct().ToArray()
            };
        }

        private static Task<ResolverProbeReport> ExecuteProbeReportAsync(
            ResolverExecutionTarget[] targets,
            string domain,
            CliOptions options,
            CancellationToken cancellationToken) {
            return ResolverProbeWorkflow.RunAsync(
                targets,
                domain,
                options.RecordType,
                Configuration.DefaultTimeout,
                CreateQueryRunOptions(options),
                CreateProbePolicy(options),
                builtInOverride: CreateBuiltInOverride(),
                explicitOverride: CreateExplicitOverride(),
                cancellationToken: cancellationToken);
        }

        private static ResolverExecutionTargetSource CreateProbeTargetSource(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return new ResolverExecutionTargetSource {
                    ResolverSelectionPath = options.ResolverUsePath
                };
            }

            if (options.HasCustomEndpointInputs) {
                return new ResolverExecutionTargetSource {
                    ResolverEndpoints = options.ProbeEndpoints.ToArray(),
                    ResolverEndpointFiles = options.ProbeEndpointFiles.ToArray(),
                    ResolverEndpointUrls = options.ProbeEndpointUrls.ToArray()
                };
            }

            return new ResolverExecutionTargetSource {
                ProbeProfile = options.Endpoint
            };
        }

        private static string? GetBenchmarkEndpointProfile(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return "selected";
            }

            return options.HasCustomEndpointInputs ? "custom" : null;
        }

        private static string GetProbeEndpointProfile(CliOptions options) {
            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath)) {
                return "selected";
            }

            return options.HasCustomEndpointInputs ? "custom" : options.Endpoint.ToString();
        }

        private static Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? CreateBuiltInOverride() {
            if (ProbeOverride == null) {
                return null;
            }

            return async (endpoint, name, recordType, cancellationToken) => CreateOverrideAttemptResult(
                endpoint.ToString(),
                await ProbeOverride(endpoint, name, cancellationToken).ConfigureAwait(false));
        }

        private static Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? CreateExplicitOverride() {
            if (ProbeEndpointOverride == null) {
                return null;
            }

            return async (endpoint, name, recordType, cancellationToken) => CreateOverrideAttemptResult(
                ResolverExecutionPlanBuilder.DescribeEndpoint(endpoint),
                await ProbeEndpointOverride(endpoint, name, cancellationToken).ConfigureAwait(false));
        }

        private static ResolverQueryAttemptResult CreateOverrideAttemptResult(
            string target,
            (DnsResponse Response, TimeSpan Elapsed, string Resolver, DnsRequestFormat RequestFormat) result) {
            return new ResolverQueryAttemptResult {
                Target = target,
                RequestFormat = result.RequestFormat,
                Resolver = result.Resolver,
                Response = result.Response,
                Elapsed = result.Elapsed
            };
        }

        private static void WriteProbeResult(ResolverProbeReportResult result) {
            foreach (string line in ResolverReportTextFormatter.BuildProbeResultLines(result, FormatDuration)) {
                Console.WriteLine(line);
            }
        }

        private static int WriteProbeSummary(ResolverProbeReport report, CliOptions options) {
            ResolverProbeEvaluation evaluation = report.Evaluation;
            ResolverProbeReportSummary summary = report.Summary;
            int successCount = summary.SuccessfulCandidates;
            Console.WriteLine("Probe Summary:");
            Console.WriteLine($"  Successful probes: {successCount}/{summary.CandidateCount}");
            Console.WriteLine($"  Failed probes: {summary.FailedCandidates}");
            Console.WriteLine($"  Fastest success: {ResolverReportTextFormatter.DescribeProbeFastestSuccess(summary, FormatDuration)}");
            Console.WriteLine($"  Fastest consensus responder: {ResolverReportTextFormatter.DescribeProbeFastestConsensus(summary, FormatDuration)}");
            Console.WriteLine($"  Transport coverage: {ResolverReportTextFormatter.DescribeProbeTransportCoverage(summary)}");
            Console.WriteLine($"  Runtime capability hints: {ResolverReportTextFormatter.DescribeProbeRuntimeCapabilities(summary)}");
            Console.WriteLine($"  Answer consensus: {ResolverReportTextFormatter.DescribeProbeAnswerConsensus(summary)}");
            Console.WriteLine($"  Mismatched responders: {ResolverReportTextFormatter.DescribeProbeMismatchedTargets(summary)}");
            Console.WriteLine($"  Distinct answer sets: {summary.DistinctAnswerSets}");
            Console.WriteLine($"  Answer variants: {ResolverReportTextFormatter.DescribeProbeAnswerVariants(summary)}");
            Console.WriteLine($"  Recommended endpoint: {ResolverReportTextFormatter.DescribeProbeRecommended(summary, FormatDuration)}");
            Console.WriteLine($"  Policy result: {(evaluation.PolicyPassed ? "pass" : $"fail ({evaluation.PolicyReason})")}");

            int exitCode = GetProbeExitCode(evaluation, options);
            if (options.ProbeSummaryLine) {
                Console.WriteLine(ResolverReportTextFormatter.BuildProbeSummaryLine(summary, exitCode));
            }

            if (!string.IsNullOrWhiteSpace(options.ProbeSavePath)) {
                SaveResolverScoreSnapshot(options.ProbeSavePath!, report.Snapshot);
            }

            return exitCode;
        }

        private static ResolverProbePolicy CreateProbePolicy(CliOptions options) {
            return new ResolverProbePolicy {
                MinSuccessCount = options.ProbeMinSuccessCount,
                MinSuccessPercent = options.ProbeMinSuccessPercent,
                MinConsensusPercent = options.ProbeMinConsensusPercent,
                RequireConsensus = options.ProbeRequireConsensus
            };
        }

        private static int GetProbeExitCode(ResolverProbeEvaluation evaluation, CliOptions options) {
            if (evaluation.SuccessfulCandidates == 0) {
                return 1;
            }

            if (options.ProbeMinSuccessCount.HasValue && evaluation.SuccessfulCandidates < options.ProbeMinSuccessCount.Value) {
                return 3;
            }

            if (options.ProbeMinSuccessPercent.HasValue && evaluation.SuccessPercent < options.ProbeMinSuccessPercent.Value) {
                return 3;
            }

            if (options.ProbeRequireConsensus && evaluation.DistinctAnswerSets > 1) {
                return 2;
            }

            if (options.ProbeMinConsensusPercent.HasValue && evaluation.ConsensusPercent < options.ProbeMinConsensusPercent.Value) {
                return 2;
            }

            return evaluation.PolicyPassed ? 0 : 1;
        }

        private static void WriteQueryResponse(DnsResponse response, CliOptions options, TimeSpan elapsed) {
            var presentationOptions = new DnsResponsePresentationOptions {
                Mode = GetResponsePresentationMode(options),
                TxtConcat = options.TxtConcatOutput,
                ShowQuestions = ShouldShowQuestionSection(options, rawDefault: false),
                ShowAnswers = ShouldShowAnswerSection(options, rawDefault: false),
                ShowAuthorities = ShouldShowAuthoritySection(options, rawDefault: false),
                ShowAdditional = ShouldShowAdditionalSection(options, rawDefault: false)
            };

            if (presentationOptions.Mode == DnsResponsePresentationMode.Raw) {
                presentationOptions.ShowQuestions = ShouldShowQuestionSection(options, rawDefault: true);
                presentationOptions.ShowAnswers = ShouldShowAnswerSection(options, rawDefault: true);
                presentationOptions.ShowAuthorities = ShouldShowAuthoritySection(options, rawDefault: true);
                presentationOptions.ShowAdditional = ShouldShowAdditionalSection(options, rawDefault: true);
            }

            foreach (string line in DnsResponseTextFormatter.BuildOutputLines(response, presentationOptions, elapsed, FormatDuration)) {
                Console.WriteLine(line);
            }
        }

        private static void WriteZoneTransferResponse(RecursiveZoneTransferResult result, CliOptions options) {
            switch (options.OutputFormat) {
                case QueryOutputFormat.Json:
                    Console.WriteLine(DnsClientXJsonSerializer.Serialize(result));
                    return;
                default:
                    WritePrettyZoneTransferResponse(result, options);
                    return;
            }
        }

        private static void SaveResolverScoreSnapshot(string path, ResolverScoreSnapshot snapshot) {
            ResolverScoreStore.Save(path, snapshot);
        }

        private static void WriteResolverSelection(ResolverSelectionResult selection, CliOptions options) {
            if (options.OutputFormat == QueryOutputFormat.Json) {
                Console.WriteLine(DnsClientXJsonSerializer.Serialize(selection));
                return;
            }

            Console.WriteLine(selection.Target);
        }

        private static void WritePrettyZoneTransferResponse(RecursiveZoneTransferResult result, CliOptions options) {
            Console.WriteLine($"Zone Transfer: {result.Zone}");
            Console.WriteLine($"  Selected authority: {result.SelectedAuthority}");
            Console.WriteLine($"  Selected server: {result.SelectedServer}:{result.Port}");
            Console.WriteLine($"  Chunks: {result.RecordSets.Length}");

            if (options.TransferSummary) {
                Console.WriteLine($"  Authorities discovered: {string.Join(", ", result.Authorities)}");
                Console.WriteLine($"  Tried servers: {string.Join(", ", result.TriedServers)}");
            }

            Console.WriteLine("Records:");
            foreach (ZoneTransferResult recordSet in result.RecordSets) {
                if (recordSet.IsOpening) {
                    Console.WriteLine("  ;; opening SOA");
                } else if (recordSet.IsClosing) {
                    Console.WriteLine("  ;; closing SOA");
                }

                foreach (DnsAnswer record in recordSet.Records) {
                    Console.WriteLine($"  {record.Name}\t{record.TTL}\tIN\t{record.Type}\t{record.Data}");
                }
            }
        }

        private static DnsResponsePresentationMode GetResponsePresentationMode(CliOptions options) {
            if (options.ShortOutput) {
                return DnsResponsePresentationMode.Short;
            }

            return options.OutputFormat switch {
                QueryOutputFormat.Json => DnsResponsePresentationMode.Json,
                QueryOutputFormat.Raw => DnsResponsePresentationMode.Raw,
                _ => DnsResponsePresentationMode.Pretty
            };
        }

        private static bool ShouldShowQuestionSection(CliOptions options, bool rawDefault) {
            if (options.HasExplicitSectionSelection) {
                return options.ShowQuestionSection;
            }

            return rawDefault;
        }

        private static bool ShouldShowAnswerSection(CliOptions options, bool rawDefault) {
            if (options.HasExplicitSectionSelection) {
                return options.ShowAnswerSection;
            }

            return !rawDefault || options.OutputFormat == QueryOutputFormat.Raw;
        }

        private static bool ShouldShowAuthoritySection(CliOptions options, bool rawDefault) {
            if (options.HasExplicitSectionSelection) {
                return options.ShowAuthoritySection;
            }

            return rawDefault;
        }

        private static bool ShouldShowAdditionalSection(CliOptions options, bool rawDefault) {
            if (options.HasExplicitSectionSelection) {
                return options.ShowAdditionalSection;
            }

            return rawDefault;
        }

        private static void WriteExplain(
            ResolverSingleOperationResult result,
            string operation,
            string target,
            DnsRecordType recordType,
            bool requestDnsSec,
            bool validateDnsSec,
            bool trace,
            string? zone = null,
            int? ttl = null) {
            foreach (string line in ResolverReportTextFormatter.BuildSingleOperationExplainLines(
                         result,
                         operation,
                         target,
                         recordType,
                         requestDnsSec,
                         validateDnsSec,
                         FormatDuration,
                         zone,
                         ttl)) {
                Console.WriteLine(line);
            }

            if (trace) {
                WriteTrace(result);
            }
        }

        private static void WriteTrace(ResolverSingleOperationResult result) {
            foreach (string line in ResolverReportTextFormatter.BuildSingleOperationTraceLines(result, FormatDuration)) {
                Console.WriteLine(line);
            }
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
            Console.WriteLine("  -t, --type <record>      DNS record type (default A, comma-separated with --benchmark)");
            Console.WriteLine("  -e, --endpoint <name>    DNS endpoint name (default System, comma-separated with --benchmark)");
            Console.WriteLine("      --domain <name>      Domain name (comma-separated with --benchmark)");
            Console.WriteLine("      --dnssec             Request DNSSEC records");
            Console.WriteLine("      --validate-dnssec    Validate DNSSEC records");
            Console.WriteLine("      --wire-post          Use DNS over HTTPS wire POST (when supported)");
            Console.WriteLine("      --format <mode>      Query output format: pretty, json, raw");
            Console.WriteLine("      --short              Print answer values only");
            Console.WriteLine("      --txt-concat         Flatten TXT output into a single string");
            Console.WriteLine("      --question           Show the question section");
            Console.WriteLine("      --answer             Show the answer section");
            Console.WriteLine("      --authority          Show the authority section");
            Console.WriteLine("      --additional         Show the additional section");
            Console.WriteLine("      --reverse            Force PTR lookup mode");
            Console.WriteLine("      --axfr               Discover authoritative servers and attempt recursive AXFR");
            Console.WriteLine("      --transfer-summary   Print discovered authorities and tried transfer targets");
            Console.WriteLine("      --benchmark          Benchmark one or more endpoints across repeated queries");
            Console.WriteLine("      --benchmark-attempts <count>  Repeat each domain/type combination this many times");
            Console.WriteLine("      --benchmark-timeout <ms>  Per-query timeout to apply during benchmark runs");
            Console.WriteLine("      --benchmark-concurrency <count>  Max concurrent benchmark queries per candidate");
            Console.WriteLine("      --benchmark-min-success-percent <percent>  Require a minimum overall benchmark success rate");
            Console.WriteLine("      --benchmark-min-successful-candidates <count>  Require this many candidates to return at least one success");
            Console.WriteLine("      --benchmark-summary-only  Suppress per-candidate benchmark rows and print only the header and summary");
            Console.WriteLine("      --benchmark-summary-line  Append one stable BENCHMARK_SUMMARY key=value line for automation");
            Console.WriteLine("      --benchmark-save <path>  Persist benchmark scoring and recommendation data as JSON");
            Console.WriteLine("      --probe              Probe the selected endpoint profile and related variants");
            Console.WriteLine("      --probe-endpoint <endpoint>  Probe a custom endpoint such as tcp@1.1.1.1:53, doq@dns.quad9.net:853, or doh3@https://dns.quad9.net/dns-query");
            Console.WriteLine("      --resolver-file <path>  Load custom endpoints from a text file for probe or benchmark");
            Console.WriteLine("      --resolver-url <url>  Load custom endpoints from an HTTP or HTTPS URL for probe or benchmark");
            Console.WriteLine("      --resolver-validate  Validate resolver endpoint inputs without querying DNS");
            Console.WriteLine("      --probe-summary-only  Suppress per-endpoint probe lines and print only the header and summary");
            Console.WriteLine("      --probe-summary-line  Append one stable PROBE_SUMMARY key=value line for automation");
            Console.WriteLine("      --probe-save <path>  Persist probe scoring and health data as JSON");
            Console.WriteLine("      --resolver-select <path>  Load a saved score snapshot and print the recommended resolver");
            Console.WriteLine("      --resolver-use <path>  Reuse the recommended resolver from a saved snapshot for query, probe, or benchmark");
            Console.WriteLine("      --probe-require-consensus    Fail when successful probe responders disagree");
            Console.WriteLine("      --probe-min-consensus <percent>  Fail when the top answer set is below the given 1-100 percentage");
            Console.WriteLine("      --probe-min-success <count>  Fail when fewer than the given number of probes succeed");
            Console.WriteLine("      --probe-min-success-percent <percent>  Fail when the probe success rate is below the given 1-100 percentage");
            Console.WriteLine("      --capabilities       Print the shared transport capability report");
            Console.WriteLine("      --stamp-info <stamp> Print parsed DNS stamp details without querying DNS");
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
