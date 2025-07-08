using System;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;

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
                    default:
                        if (domain is null) {
                            domain = args[i];
                        } else {
                            Console.Error.WriteLine($"Unknown argument: {args[i]}");
                            return 1;
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(domain)) {
                Console.Error.WriteLine("Domain name is required.");
                return 1;
            }

            try {
                await using var client = new ClientX(endpoint);
                var response = await client.Resolve(domain, recordType, cancellationToken: cts.Token);
                Console.WriteLine($"Status: {response.Status}");
                foreach (var answer in response.Answers) {
                    Console.WriteLine($"{answer.Name}\t{answer.Type}\t{answer.TTL}\t{answer.Data}");
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
            Console.WriteLine();
            Console.WriteLine("Available endpoints:");
            foreach (var (ep, desc) in DnsEndpointExtensions.GetAllWithDescriptions()) {
                Console.WriteLine($"  {ep,-20} {desc}");
            }
        }
    }
}
