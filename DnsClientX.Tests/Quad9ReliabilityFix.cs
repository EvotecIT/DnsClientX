using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DnsClientX.Tests {
    /// <summary>
    /// Exploratory tests demonstrating retry logic for unreliable Quad9 DNS endpoints.
    /// </summary>
    public class Quad9ReliabilityFix {
        private readonly ITestOutputHelper output;

        public Quad9ReliabilityFix(ITestOutputHelper output) {
            this.output = output;
        }

        /// <summary>
        /// Demonstrates retry logic when querying Quad9 which tends to throttle automated requests.
        /// </summary>
        [Theory(Skip = "External dependency - unreliable for automated testing")]
        [InlineData(DnsEndpoint.Quad9)]
        [InlineData(DnsEndpoint.Quad9ECS)]
        [InlineData(DnsEndpoint.Quad9Unsecure)]
        public async Task TestQuad9WithRetryLogic(DnsEndpoint endpoint) {
            var domain = "github.com";
            var success = false;
            var maxAttempts = 5;
            var attempt = 0;

            output.WriteLine($"Testing {endpoint} with retry logic for {domain}");

            // Retry with exponential backoff specifically for Quad9
            while (!success && attempt < maxAttempts) {
                attempt++;
                try {
                    // Create a fresh client for each attempt to avoid connection reuse issues
                    using var client = new ClientX(endpoint) {
                        Debug = false
                    };

                    var response = await client.Resolve(domain, DnsRecordType.A);

                    if (response.Status == DnsResponseCode.NoError && response.Answers?.Length > 0) {
                        success = true;
                        output.WriteLine($"  ✅ Success on attempt {attempt}: {response.Answers.Length} answers");
                    } else {
                        output.WriteLine($"  ❌ Attempt {attempt} failed: Status={response.Status}, Answers={response.Answers?.Length ?? 0}");
                    }
                } catch (Exception ex) {
                    output.WriteLine($"  ❌ Attempt {attempt} exception: {ex.GetType().Name}: {ex.Message}");
                }

                if (!success && attempt < maxAttempts) {
                    // Exponential backoff: 500ms, 1s, 2s, 4s
                    var delay = (int)(500 * Math.Pow(2, attempt - 1));
                    output.WriteLine($"  ⏱️ Waiting {delay}ms before retry...");
                    await Task.Delay(delay);
                }
            }

            // For Quad9, we accept success after retries due to known reliability issues
            Assert.True(success, $"{endpoint} failed after {maxAttempts} attempts. " +
                               $"This may be due to Quad9's rate limiting or connection handling. " +
                               $"Consider using Cloudflare or Google DNS for more reliable automated testing.");
        }

        /// <summary>
        /// Checks that recommended providers respond reliably for automated tests.
        /// </summary>
        [Fact(Skip = "External dependency - unreliable for automated testing")]
        public async Task RecommendedReliableProvidersTest() {
            // Test providers that are more reliable for automated testing
            var reliableProviders = new[] {
                DnsEndpoint.Cloudflare,
                DnsEndpoint.Google,
                DnsEndpoint.OpenDNS
            };

            var domain = "github.com";

            output.WriteLine("Testing reliable providers for automated testing:");
            output.WriteLine(new string('=', 50));

            foreach (var provider in reliableProviders) {
                using var client = new ClientX(provider) { Debug = false };

                try {
                    var response = await client.Resolve(domain, DnsRecordType.A);
                    var success = response.Status == DnsResponseCode.NoError && response.Answers?.Length > 0;

                    output.WriteLine($"{provider}: {(success ? "✅ Reliable" : "❌ Failed")} " +
                                   $"({response.Answers?.Length ?? 0} answers)");

                    // These providers should be reliable
                    Assert.True(success, $"{provider} should be reliable for automated testing");

                } catch (Exception ex) {
                    output.WriteLine($"{provider}: ❌ Exception - {ex.GetType().Name}: {ex.Message}");
                    Assert.Fail($"{provider} threw exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Outputs troubleshooting information about Quad9 reliability in automated scenarios.
        /// </summary>
        [Fact(Skip = "External dependency - unreliable for automated testing")]
        public void ExplainQuad9Issues() {
            output.WriteLine("Quad9 Reliability Issues Explanation:");
            output.WriteLine(new string('=', 40));
            output.WriteLine("");
            output.WriteLine("❌ PROBLEM: Quad9 DNS providers show 43-70% reliability in automated tests");
            output.WriteLine("");
            output.WriteLine("🔍 ROOT CAUSE:");
            output.WriteLine("  • Rate limiting - blocks rapid successive requests");
            output.WriteLine("  • Anti-DDoS protection - treats automation as suspicious");
            output.WriteLine("  • Connection handling - drops/resets connections aggressively");
            output.WriteLine("");
            output.WriteLine("✅ SOLUTIONS:");
            output.WriteLine("  1. Use retry logic with exponential backoff for Quad9");
            output.WriteLine("  2. Create fresh connections (don't reuse HTTP clients)");
            output.WriteLine("  3. Add delays between requests");
            output.WriteLine("  4. For automated testing, prefer:");
            output.WriteLine("     • Cloudflare DNS (1.1.1.1) - Most reliable");
            output.WriteLine("     • Google DNS (8.8.8.8) - Very reliable");
            output.WriteLine("     • OpenDNS - Generally reliable");
            output.WriteLine("");
            output.WriteLine("📝 NOTE: This is a Quad9 server-side limitation, not a DnsClientX bug.");
            output.WriteLine("         Manual/interactive usage may have better success rates.");
        }
    }
}