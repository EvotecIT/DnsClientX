using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static partial class Program {
        private static bool TryParseArgs(string[] args, out CliOptions? options, out string? errorMessage, out IReadOnlyList<string>? invalidSwitches) {
            options = new CliOptions();
            errorMessage = null;
            var unknown = new List<string>();

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                if (arg.StartsWith("-", StringComparison.Ordinal)) options.ProvidedSwitches.Add(arg);
                switch (arg) {
                    case var opt when opt.Equals("--profile", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--profile", out string? profile, out errorMessage)) { invalidSwitches = null; options = null; return false; }
                        options.ProfilePath = profile;
                        break;
                    case var opt when opt.Equals("--profile-save", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--profile-save", out string? profileSave, out errorMessage)) { invalidSwitches = null; options = null; return false; }
                        options.ProfileSavePath = profileSave;
                        break;
                    case var opt when opt.Equals("--resolver-compare", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--resolver-compare", out string? baseline, out errorMessage) || !TryReadNext(args, ref i, "--resolver-compare", out string? current, out errorMessage)) { invalidSwitches = null; options = null; return false; }
                        options.CompareBaselinePath = baseline;
                        options.CompareCurrentPath = current;
                        break;
                    case var opt when opt.Equals("--reference-response", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--reference-response", out string? reference, out errorMessage)) { invalidSwitches = null; options = null; return false; }
                        options.ReferenceResponsePath = reference;
                        break;
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
                    case var opt when opt.Equals("--dnssec-verifier", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--dnssec-verifier", out string? verifierPath, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        if (string.IsNullOrWhiteSpace(verifierPath)) {
                            errorMessage = "--dnssec-verifier requires a nonblank local DLL path.";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.DnsSecVerifierPath = verifierPath;
                        break;
                    case var opt when opt.Equals("--wire-post", StringComparison.OrdinalIgnoreCase):
                        options.WirePost = true;
                        break;
                    case var opt when opt.Equals("--nsid", StringComparison.OrdinalIgnoreCase):
                        options.RequestNsid = true;
                        break;
                    case var opt when opt.Equals("--bootstrap", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--bootstrap", out string? bootstrap, out errorMessage)) {
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        try { options.BootstrapResolver = EndpointParser.ParseBootstrap(bootstrap!); }
                        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) {
                            errorMessage = ex.Message;
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
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
                    case var opt when opt.Equals("--benchmark-connections", StringComparison.OrdinalIgnoreCase):
                        if (!TryReadNext(args, ref i, "--benchmark-connections", out string? connectionMode, out errorMessage) ||
                            (connectionMode != "cold" && connectionMode != "warm") ||
                            !Enum.TryParse(connectionMode, true, out ResolverQueryConnectionMode parsedMode) ||
                            (parsedMode != ResolverQueryConnectionMode.Cold && parsedMode != ResolverQueryConnectionMode.Warm)) {
                            errorMessage = "--benchmark-connections accepts cold or warm.";
                            invalidSwitches = null;
                            options = null;
                            return false;
                        }
                        options.Benchmark = true;
                        options.BenchmarkConnectionMode = parsedMode;
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
                    case var opt when opt.Equals("--probe-endpoint", StringComparison.OrdinalIgnoreCase) || opt.Equals("--resolver", StringComparison.OrdinalIgnoreCase):
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

            if (!ValidateParsedOptions(options, out errorMessage)) {
                invalidSwitches = null;
                options = null;
                return false;
            }

            invalidSwitches = Array.Empty<string>();
            return true;
        }

    }
}
