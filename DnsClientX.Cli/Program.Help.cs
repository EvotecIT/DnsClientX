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
        private static void ShowHelp() {
            Console.WriteLine("DnsClientX.Cli - simple DNS query tool");
            Console.WriteLine("Usage: DnsClientX.Cli [options] <domain>");
            Console.WriteLine("  --resolver <endpoint>              Query one explicit resolver directly (UDP/TCP/DoT/DoH/DoQ etc.)");
            Console.WriteLine("  --profile <file> [--benchmark]      Run a versioned resolver health profile; settings come from its JSON");
            Console.WriteLine("  --profile-save <file>               Save the complete health report JSON");
            Console.WriteLine("  --resolver-compare <old> <new>      Compare saved probe/benchmark snapshots");
            Console.WriteLine("  --reference-response <file>         Caller-supplied authoritative reference array for comparison");
            Console.WriteLine("  Explicit resolver inputs query directly; add --probe for multiple-candidate probing.");
            Console.WriteLine("  --format json emits a complete JSON report; --explain/--trace query JSON includes structured evidence.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -t, --type <record>      DNS record type (default A, comma-separated with --benchmark)");
            Console.WriteLine("  -e, --endpoint <name>    DNS endpoint name (default System, comma-separated with --benchmark)");
            Console.WriteLine("      --domain <name>      Domain name (comma-separated with --benchmark)");
            Console.WriteLine("      --dnssec             Request DNSSEC records");
            Console.WriteLine("      --validate-dnssec    Validate DNSSEC records");
            Console.WriteLine("      --dnssec-verifier    Optional DNSSEC provider DLL path (dependencies beside it)");
            Console.WriteLine("      --wire-post          Use DNS over HTTPS wire POST (when supported)");
            Console.WriteLine("      --bootstrap <endpoint>  Resolve endpoint hostnames through an IP-literal UDP/TCP resolver, e.g. udp@1.1.1.1:53");
            Console.WriteLine("      --nsid               Request resolver identity through EDNS (wire transports)");
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
            Console.WriteLine("      --benchmark-concurrency <count>  Max concurrent queries across the benchmark run");
            Console.WriteLine("      --benchmark-connections <cold|warm>  Fresh clients per attempt or retained clients per target (default cold)");
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
            Console.WriteLine("      --update <zone> <name> <type> <data>  Send dynamic update to one explicit authoritative endpoint");
            Console.WriteLine("      --ttl <seconds>      TTL for update (default 300)");
            Console.WriteLine();
            Console.WriteLine("Available endpoints:");
            foreach (var (ep, desc) in DnsEndpointExtensions.GetAllWithDescriptions()) {
                Console.WriteLine($"  {ep,-20} {desc}");
            }
        }
    }
}
