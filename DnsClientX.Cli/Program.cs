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
                    WriteHumanError(errorMessage);
                }

                if (invalidSwitches != null) {
                    foreach (string invalid in invalidSwitches) {
                        WriteHumanError($"Unknown argument: {invalid}");
                    }
                }

                ShowHelp();
                return 1;
            }

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, e) => {
                e.Cancel = true;
                cts.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try {
                CliOptions parsedOptions = options!;
                if (parsedOptions.ProfilePath != null) return await RunProfileAsync(parsedOptions, cts.Token).ConfigureAwait(false);
                if (parsedOptions.CompareBaselinePath != null) return RunSnapshotComparison(parsedOptions);
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
                WriteHumanError(ex.Message);
                return 1;
            } finally {
                Console.CancelKeyPress -= cancelHandler;
            }
        }

    }
}
