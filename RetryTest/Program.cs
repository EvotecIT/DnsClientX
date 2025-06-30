using System;
using System.Threading.Tasks;
using DnsClientX;

namespace RetryTest {
    class Program {
        static async Task Main(string[] args) {
            Console.WriteLine("Testing Quad9 DNS servers for empty response patterns...");

            var endpoints = new[] {
                ("Cloudflare", DnsEndpoint.Cloudflare),
                ("Quad9", DnsEndpoint.Quad9),
                ("Quad9ECS", DnsEndpoint.Quad9ECS),
                ("Quad9Unsecure", DnsEndpoint.Quad9Unsecure)
            };

            var testCases = new[] {
                ("evotec.pl", DnsRecordType.MX),
                ("evotec.pl", DnsRecordType.AAAA),
                ("autodiscover.evotec.pl", DnsRecordType.CNAME)
            };

            foreach (var (domain, recordType) in testCases) {
                Console.WriteLine($"\n=== Testing {domain} / {recordType} ===");

                foreach (var (name, endpoint) in endpoints) {
                    try {
                        var client = new ClientX(endpoint);

                        // Test ResolveAll which is what the failing test uses
                        var answers = await client.ResolveAll(domain, recordType).ConfigureAwait(false);

                        Console.WriteLine($"{name}: {answers.Length} records");
                        if (answers.Length == 0) {
                            // Get the full response to see status code
                            var fullResponse = await client.Resolve(domain, recordType).ConfigureAwait(false);
                            Console.WriteLine($"  Status: {fullResponse.Status}");
                            Console.WriteLine($"  Error: {fullResponse.Error ?? "None"}");
                        } else {
                            foreach (var answer in answers) {
                                Console.WriteLine($"  - {answer.Data}");
                            }
                        }

                    } catch (Exception ex) {
                        Console.WriteLine($"{name}: EXCEPTION - {ex.Message}");
                    }
                }
            }

            Console.WriteLine("\nDone testing empty response patterns.");
        }
    }
}
