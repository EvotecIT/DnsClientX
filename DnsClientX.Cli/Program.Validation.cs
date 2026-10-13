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
        private static bool ValidateParsedOptions(CliOptions options, out string? errorMessage) {
            errorMessage = null;
            if (!ValidateOperationalMode(options, out errorMessage)) {
                return false;
            }
            if (options.ProfilePath != null || options.CompareBaselinePath != null) {
                return true;
            }
            if (options.HasCustomEndpointInputs && options.EndpointInputs.Count > 0) {
                errorMessage = "Use either --endpoint or explicit resolver inputs.";
                return false;
            }

            if (options.ShowCapabilities &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || !string.IsNullOrWhiteSpace(options.ResolverUsePath) ||
                 !string.IsNullOrWhiteSpace(options.StampInfo) || options.ResolverValidate)) {
                errorMessage = "--capabilities cannot be combined with query, probe, benchmark, update, axfr, resolver selection, or stamp modes.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.DnsSecVerifierPath) &&
                (options.ShowCapabilities || !string.IsNullOrWhiteSpace(options.StampInfo) || options.ResolverValidate ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || options.ZoneTransfer || options.DoUpdate)) {
                errorMessage = "--dnssec-verifier applies only to query, probe, and benchmark modes.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.StampInfo) &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || !string.IsNullOrWhiteSpace(options.ResolverUsePath) ||
                 options.ResolverValidate)) {
                errorMessage = "--stamp-info cannot be combined with query, probe, benchmark, update, axfr, or resolver selection modes.";
                return false;
            }

            if (options.ResolverValidate &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer ||
                 !string.IsNullOrWhiteSpace(options.ResolverSelectPath) || !string.IsNullOrWhiteSpace(options.ResolverUsePath))) {
                errorMessage = "--resolver-validate cannot be combined with query, probe, benchmark, update, axfr, or resolver selection modes.";
                return false;
            }

            if (options.DoUpdate && (options.Probe || options.Benchmark || options.ZoneTransfer)) {
                errorMessage = options.Benchmark
                    ? "--benchmark cannot be combined with --update."
                    : options.Probe
                        ? "--probe cannot be combined with --update."
                        : "--axfr cannot be combined with --update.";
                return false;
            }

            if (options.Probe && options.Benchmark) {
                errorMessage = "--probe cannot be combined with --benchmark.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) &&
                (options.Probe || options.Benchmark || options.DoUpdate || options.ZoneTransfer)) {
                errorMessage = "--resolver-select cannot be combined with probe, benchmark, update, or axfr modes.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath) &&
                (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) || options.DoUpdate || options.ZoneTransfer)) {
                errorMessage = "--resolver-use cannot be combined with --resolver-select, update, or axfr modes.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath) &&
                (options.EndpointInputs.Count > 0 || options.HasCustomEndpointInputs)) {
                errorMessage = "--resolver-use cannot be combined with --endpoint, --probe-endpoint, --resolver-file, or --resolver-url.";
                return false;
            }

            if (options.ZoneTransfer && (options.Probe || options.Benchmark)) {
                errorMessage = options.Probe
                    ? "--axfr cannot be combined with --probe."
                    : "--axfr cannot be combined with --benchmark.";
                return false;
            }

            if (options.ZoneTransfer && options.ReverseLookup) {
                errorMessage = "--reverse cannot be combined with --axfr.";
                return false;
            }

            if (!ApplyReverseLookupDefaults(options, out errorMessage)) {
                return false;
            }

            if (options.ShortOutput && options.OutputFormat != QueryOutputFormat.Pretty) {
                errorMessage = "--short cannot be combined with --format json or --format raw.";
                return false;
            }

            if (options.TxtConcatOutput && options.OutputFormat == QueryOutputFormat.Json) {
                errorMessage = "--txt-concat cannot be combined with --format json.";
                return false;
            }

            if (options.ShowCapabilities &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw)) {
                errorMessage = "Capability mode supports only default output or --format json.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.StampInfo) &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw ||
                 options.ReverseLookup || options.RecordTypeSpecified || options.RequestDnsSec || options.ValidateDnsSec || options.WirePost ||
                 !string.IsNullOrWhiteSpace(options.Domain))) {
                errorMessage = "Stamp mode supports only --stamp-info and optional --format json.";
                return false;
            }

            if (options.ResolverValidate &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw ||
                 options.ReverseLookup || options.RecordTypeSpecified || options.RequestDnsSec || options.ValidateDnsSec || options.WirePost ||
                 !string.IsNullOrWhiteSpace(options.Domain) || !options.HasCustomEndpointInputs)) {
                errorMessage = "Resolver validation mode requires --resolver-validate with --probe-endpoint, --resolver-file, or --resolver-url, and supports optional --format json.";
                return false;
            }

            if ((options.Benchmark || options.Probe || options.DoUpdate) &&
                (options.ShortOutput || options.TxtConcatOutput || options.OutputFormat == QueryOutputFormat.Raw || options.DoUpdate && options.OutputFormat == QueryOutputFormat.Json || options.HasExplicitSectionSelection || options.TransferSummary)) {
                errorMessage = "Probe and benchmark support pretty or JSON reports. Query section, raw, short, and TXT switches apply only to queries; updates use pretty output.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw)) {
                errorMessage = "Resolver selection mode supports only default output or --format json.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverUsePath) &&
                (options.TransferSummary || options.OutputFormat == QueryOutputFormat.Raw && (options.Probe || options.Benchmark))) {
                errorMessage = "Resolver reuse mode does not change the output rules of probe or benchmark workflows.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(options.ResolverSelectPath) &&
                (options.ReverseLookup || options.RecordTypeSpecified || options.RequestDnsSec || options.ValidateDnsSec || options.WirePost)) {
                errorMessage = "Query switches do not apply to --resolver-select.";
                return false;
            }

            if (options.ZoneTransfer &&
                (options.ShortOutput || options.TxtConcatOutput || options.HasExplicitSectionSelection || options.OutputFormat == QueryOutputFormat.Raw)) {
                errorMessage = "AXFR mode supports only --format pretty|json and optional --transfer-summary.";
                return false;
            }

            if (options.TransferSummary && !options.ZoneTransfer) {
                errorMessage = "--transfer-summary applies only to --axfr mode.";
                return false;
            }

            if (options.ZoneTransfer && options.RecordTypeSpecified && options.RecordType != DnsRecordType.AXFR) {
                errorMessage = "--type can be used with --axfr only when set to AXFR.";
                return false;
            }

            if (options.DoUpdate) {
                if (!options.HasCustomEndpointInputs || options.EndpointInputs.Count > 0) {
                    errorMessage = "--update requires an explicit authoritative endpoint via --probe-endpoint, --resolver-file, or --resolver-url; do not combine it with --endpoint.";
                            return false;
                }
                if (options.Zone is null || options.UpdateName is null || options.UpdateData is null) {
                    errorMessage = "Invalid --update arguments.";
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
                return false;
            } else if (!options.Probe && !options.Benchmark && string.IsNullOrWhiteSpace(options.Domain)) {
                errorMessage = "Domain name is required.";
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

            return true;
        }

    }
}
