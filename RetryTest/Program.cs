using System;
using System.Threading.Tasks;
using DnsClientX;

namespace RetryTest {
    class Program {
        static async Task Main(string[] args) {
            var logger = new InternalLogger(true) { IsInformation = true };
            logger.WriteInformation("Testing Quad9 DNS servers for empty response patterns...");

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
                logger.WriteInformation($"\n=== Testing {domain} / {recordType} ===");

                foreach (var (name, endpoint) in endpoints) {
                    try {
                        var client = new ClientX(endpoint);

                        // Test ResolveAll which is what the failing test uses
                        var answers = await client.ResolveAll(domain, recordType);

                        logger.WriteInformation($"{name}: {answers.Length} records");
                        if (answers.Length == 0) {
                            // Get the full response to see status code
                            var fullResponse = await client.Resolve(domain, recordType);
                            logger.WriteInformation($"  Status: {fullResponse.Status}");
                            logger.WriteInformation($"  Error: {fullResponse.Error ?? "None"}");
                        } else {
                            foreach (var answer in answers) {
                                logger.WriteInformation($"  - {answer.Data}");
                            }
                        }

                    } catch (Exception ex) {
                        logger.WriteError($"{name}: EXCEPTION - {ex.Message}");
                    }
                }
            }

            logger.WriteInformation("\nDone testing empty response patterns.");
        }
    }
}
