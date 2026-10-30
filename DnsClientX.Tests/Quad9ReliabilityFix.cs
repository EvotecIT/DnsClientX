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

        /// <summary>
        /// Initializes a new instance of the <see cref="Quad9ReliabilityFix"/> class.
        /// </summary>
        /// <param name="output">The output helper used for test diagnostics.</param>
        public Quad9ReliabilityFix(ITestOutputHelper output) {
            this.output = output;
        }

        /// <summary>
        /// Demonstrates retry logic when querying Quad9 which tends to throttle automated requests.
        /// </summary>
        [RealDnsTheory]
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
        [RealDnsFact]
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

    }
}
