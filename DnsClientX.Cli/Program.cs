using DnsClientX;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static class Program {
        private static async Task<int> Main(string[] args) {
            if (args.Length == 0 ||
                string.Equals(args[0], "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args[0], "--help", StringComparison.OrdinalIgnoreCase)) {
                ShowHelp();
                return 0;
            }

            string? domain = null;
            DnsRecordType recordType = DnsRecordType.A;
            DnsEndpoint endpoint = DnsEndpoint.System;
            bool requestDnsSec = false;
            bool validateDnsSec = false;
            bool wirePost = false;
            bool doUpdate = false;
            string? zone = null;
            string? updateName = null;
            string? updateData = null;
            int ttl = 300;

            var invalidSwitches = new List<string>();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => {
                e.Cancel = true;
                cts.Cancel();
            };

            for (int i = 0; i < args.Length; i++) {
                switch (args[i]) {
                    case var opt when opt.Equals("-t", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--type", StringComparison.OrdinalIgnoreCase):
                        if (i + 1 >= args.Length) {
                            Console.Error.WriteLine("Missing value for --type");
                            return 1;
                        }
                        recordType = (DnsRecordType)Enum.Parse(typeof(DnsRecordType), args[++i], true);
                        break;
                    case var opt when opt.Equals("-e", StringComparison.OrdinalIgnoreCase) ||
                                       opt.Equals("--endpoint", StringComparison.OrdinalIgnoreCase):
                        if (i + 1 >= args.Length) {
                            Console.Error.WriteLine("Missing value for --endpoint");
                            return 1;
                        }
                        endpoint = (DnsEndpoint)Enum.Parse(typeof(DnsEndpoint), args[++i], true);
                        break;
                    case var opt when opt.Equals("--dnssec", StringComparison.OrdinalIgnoreCase):
                        requestDnsSec = true;
                        break;
                    case var opt when opt.Equals("--validate-dnssec", StringComparison.OrdinalIgnoreCase):
                        validateDnsSec = true;
                        break;
                    case var opt when opt.Equals("--wire-post", StringComparison.OrdinalIgnoreCase):
                        wirePost = true;
                        break;
                    case var opt when opt.Equals("--update", StringComparison.OrdinalIgnoreCase):
                        if (i + 4 >= args.Length) {
                            Console.Error.WriteLine("Missing values for --update");
                            return 1;
                        }
                        doUpdate = true;
                        zone = args[++i];
                        updateName = args[++i];
                        recordType = (DnsRecordType)Enum.Parse(typeof(DnsRecordType), args[++i], true);
                        updateData = args[++i];
                        break;
                    case var opt when opt.Equals("--ttl", StringComparison.OrdinalIgnoreCase):
                        if (i + 1 >= args.Length) {
                            Console.Error.WriteLine("Missing value for --ttl");
                            return 1;
                        }
                        ttl = int.Parse(args[++i]);
                        break;
                    default:
                        if (domain is null && !args[i].StartsWith("-", StringComparison.Ordinal)) {
                            domain = args[i];
                        } else {
                            invalidSwitches.Add(args[i]);
                        }
                        break;
                }
            }

            if (invalidSwitches.Count > 0) {
                foreach (string invalid in invalidSwitches) {
                    Console.Error.WriteLine($"Unknown argument: {invalid}");
                }
                ShowHelp();
                return 1;
            }

            if (doUpdate) {
                if (zone is null || updateName is null || updateData is null) {
                    Console.Error.WriteLine("Invalid --update arguments.");
                    return 1;
                }
            } else if (string.IsNullOrWhiteSpace(domain)) {
                Console.Error.WriteLine("Domain name is required.");
                return 1;
            }

            try {
                await using var client = new ClientX(endpoint);
                string? envPort = Environment.GetEnvironmentVariable("DNSCLIENTX_CLI_PORT");
                if (int.TryParse(envPort, out int customPort) && customPort > 0) {
                    client.EndpointConfiguration.Port = customPort;
                }
                if (wirePost &&
                    (client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttps ||
                     client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsPOST ||
                     client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSON ||
                     client.EndpointConfiguration.RequestFormat == DnsRequestFormat.DnsOverHttpsJSONPOST)) {
                    client.EndpointConfiguration.RequestFormat = DnsRequestFormat.DnsOverHttpsWirePost;
                }
                if (doUpdate) {
                    var response = await client.UpdateRecordAsync(zone!, updateName!, recordType, updateData!, ttl, cts.Token);
                    Console.WriteLine($"Update status: {response.Status} (retries {response.RetryCount})");
                } else {
                    var response = await client.Resolve(domain, recordType, requestDnsSec, validateDnsSec, cancellationToken: cts.Token);
                    Console.WriteLine($"Status: {response.Status} (retries {response.RetryCount})");
                    foreach (var answer in response.Answers) {
                        Console.WriteLine($"{answer.Name}\t{answer.Type}\t{answer.TTL}\t{answer.Data}");
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
            Console.WriteLine("      --update <zone> <name> <type> <data>  Send dynamic update");
            Console.WriteLine("      --ttl <seconds>       TTL for update (default 300)");
            Console.WriteLine();
            Console.WriteLine("Available endpoints:");
            foreach (var (ep, desc) in DnsEndpointExtensions.GetAllWithDescriptions()) {
                Console.WriteLine($"  {ep,-20} {desc}");
            }
        }
    }
}
