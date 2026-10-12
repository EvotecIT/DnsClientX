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
                if (options.Explain) {
                    ResolverScoreSnapshot snapshot = ResolverScoreStore.Load(options.ResolverSelectPath!);
                    Console.WriteLine(DnsClientXJsonSerializer.Serialize(new { Selection = selection, snapshot.Summary, snapshot.Context }));
                } else {
                    Console.WriteLine(DnsClientXJsonSerializer.Serialize(selection));
                }
                return;
            }

            WriteHumanLine(selection.Target);
        }

        private static void WritePrettyZoneTransferResponse(RecursiveZoneTransferResult result, CliOptions options) {
            WriteHumanLine($"Zone Transfer: {result.Zone}");
            WriteHumanLine($"  Selected authority: {result.SelectedAuthority}");
            WriteHumanLine($"  Selected server: {result.SelectedServer}:{result.Port}");
            Console.WriteLine($"  Chunks: {result.RecordSets.Length}");

            if (options.TransferSummary) {
                WriteHumanLine($"  Authorities discovered: {string.Join(", ", result.Authorities)}");
                WriteHumanLine($"  Tried servers: {string.Join(", ", result.TriedServers)}");
            }

            Console.WriteLine("Records:");
            foreach (ZoneTransferResult recordSet in result.RecordSets) {
                if (recordSet.IsOpening) {
                    Console.WriteLine("  ;; opening SOA");
                } else if (recordSet.IsClosing) {
                    Console.WriteLine("  ;; closing SOA");
                }

                foreach (DnsAnswer record in recordSet.Records) {
                    Console.WriteLine($"  {DnsTerminalText.Escape(record.Name)}\t{record.TTL}\tIN\t{record.Type}\t{DnsTerminalText.Escape(record.Data)}");
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

        private static void WriteHumanLine(string value) => Console.WriteLine(DnsTerminalText.Escape(value));

        private static void WriteHumanError(string? value) => Console.Error.WriteLine(DnsTerminalText.Escape(value));

    }
}
