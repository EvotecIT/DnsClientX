using System;
using System.Threading;
using System.Threading.Tasks;
using DnsClientX;

namespace RetryTest {
    class Program {
        static async Task Main(string[] args) {
            // Cancel all DNS operations after a timeout to prevent endless waits.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await RunAsync(cts.Token);
        }

        /// <summary>
        /// Executes the DNS tests. Pass a cancellation token to control the lifetime.
        /// </summary>
        /// <remarks>
        /// Example:
        /// <code>
        /// using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        /// await Program.RunAsync(cts.Token);
        /// </code>
        /// </remarks>
        internal static async Task RunAsync(CancellationToken cancellationToken) {
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
                cancellationToken.ThrowIfCancellationRequested();
                logger.WriteInformation($"\n=== Testing {domain} / {recordType} ===");

                foreach (var (name, endpoint) in endpoints) {
                    try {
                        var client = new ClientX(endpoint);

                        // Test ResolveAll which is what the failing test uses
                        var answers = await client.ResolveAll(domain, recordType, cancellationToken: cancellationToken);

                        logger.WriteInformation($"{name}: {answers.Length} records");
                        if (answers.Length == 0) {
                            // Get the full response to see status code
                            var fullResponse = await client.Resolve(domain, recordType, cancellationToken: cancellationToken);
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
