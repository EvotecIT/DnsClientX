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
        private static ResolverExecutionClientOptions CreateExecutionClientOptions(CliOptions options, bool useBenchmarkTimeout = false) {
            if (!string.IsNullOrWhiteSpace(options.DnsSecVerifierPath)) {
                options.DnsSecSignatureVerifier ??= DnsSecSignatureVerifierLoader.Load(options.DnsSecVerifierPath!);
            }
            string? envPort = Environment.GetEnvironmentVariable("DNSCLIENTX_CLI_PORT");
            int? customPort = int.TryParse(envPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort) && parsedPort > 0
                ? parsedPort
                : null;

            return new ResolverExecutionClientOptions {
                EnableAudit = options.Explain || options.Trace,
                TimeoutMs = useBenchmarkTimeout ? options.BenchmarkTimeoutMs : null,
                PortOverride = customPort,
                ForceDohWirePost = options.WirePost,
                BootstrapResolver = options.BootstrapResolver,
                DnsSecSignatureVerifier = options.DnsSecSignatureVerifier,
                RequestNsid = options.RequestNsid
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

                if (options.OutputFormat == QueryOutputFormat.Json && options.Explain) {
                    Console.WriteLine(DnsClientXJsonSerializer.Serialize(new {
                        Response = response,
                        Explanation = DnsQueryExplanation.Create(response, result.AuditTrail),
                        result.SelectionStrategy, result.RequestFormat, result.CacheEnabled,
                        result.ConfiguredResolverHost, result.ConfiguredResolverPort,
                        ElapsedMs = result.Elapsed.TotalMilliseconds
                    }));
                } else {
                    WriteQueryResponse(response, options, result.Elapsed);
                }

                if ((options.Explain || options.Trace) && options.OutputFormat != QueryOutputFormat.Json) {
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

            if (!string.IsNullOrWhiteSpace(response.Error)) {
                WriteHumanError(response.Error);
                return 1;
            }
            return 0;
        }

        private static ResolverExecutionTargetSource CreateStandardQueryTargetSource(CliOptions options) {
            if (options.HasCustomEndpointInputs) {
                return new ResolverExecutionTargetSource {
                    ResolverEndpoints = options.ProbeEndpoints.ToArray(),
                    ResolverEndpointFiles = options.ProbeEndpointFiles.ToArray(),
                    ResolverEndpointUrls = options.ProbeEndpointUrls.ToArray()
                };
            }
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
                WriteHumanLine(line);
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
            WriteHumanLine($"  Host: {info.Host}");
            Console.WriteLine($"  Port: {info.Port}");
            if (info.DohUrl != null) {
                WriteHumanLine($"  DoH URL: {info.DohUrl}");
            }
            Console.WriteLine($"  DNSSEC property: {(info.DnsSecOk ? "yes" : "no")}");
            WriteHumanLine($"  Endpoint: {info.Endpoint}");
            WriteHumanLine($"  Normalized stamp: {info.NormalizedStamp}");
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
                    WriteHumanLine($"  valid   {location}  {result.Entry} -> {result.Endpoint}");
                } else {
                    WriteHumanLine($"  invalid {location}  {result.Entry} ({result.Error})");
                }
            }
        }

    }
}
