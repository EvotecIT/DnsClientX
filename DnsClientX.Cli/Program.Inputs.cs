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

    }
}
