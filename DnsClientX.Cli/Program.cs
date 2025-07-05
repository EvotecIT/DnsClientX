using System;
using System.Threading.Tasks;

namespace DnsClientX.Cli {
    internal static class Program {
        private static async Task<int> Main(string[] args) {
            if (args.Length == 0 || args[0] is "-h" or "--help") {
                ShowHelp();
                return 0;
            }

            string? domain = null;
            DnsRecordType recordType = DnsRecordType.A;
            DnsEndpoint endpoint = DnsEndpoint.System;

            for (int i = 0; i < args.Length; i++) {
                switch (args[i]) {
                    case "-t":
                    case "--type":
                        if (i + 1 >= args.Length) {
                            Console.Error.WriteLine("Missing value for --type");
                            return 1;
                        }
                        recordType = (DnsRecordType)Enum.Parse(typeof(DnsRecordType), args[++i], true);
                        break;
                    case "-e":
                    case "--endpoint":
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
                var response = await ClientX.QueryDns(domain, recordType, endpoint);
                Console.WriteLine($"Status: {response.Status}");
                foreach (var answer in response.Answers) {
                    Console.WriteLine($"{answer.Name}\t{answer.Type}\t{answer.TTL}\t{answer.Data}");
                }
                return 0;
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
        }
    }
}
