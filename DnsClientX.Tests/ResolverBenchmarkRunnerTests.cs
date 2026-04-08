using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver benchmark execution.
    /// </summary>
    public class ResolverBenchmarkRunnerTests {
        /// <summary>
        /// Ensures benchmark execution expands attempts per combination and reports progress.
        /// </summary>
        [Fact]
        public async Task RunAsync_UsesOverridesAndProducesExpectedAttemptCount() {
            ResolverExecutionTarget[] targets = {
                new ResolverExecutionTarget {
                    DisplayName = "Cloudflare",
                    BuiltInEndpoint = DnsEndpoint.Cloudflare
                }
            };

            var progress = new List<(int Completed, int Total)>();
            ResolverQueryAttemptResult[] results = await ResolverBenchmarkRunner.RunAsync(
                targets,
                new[] { "example.com", "example.net" },
                new[] { DnsRecordType.A },
                attemptsPerCombination: 2,
                maxConcurrency: 2,
                new ResolverQueryRunOptions {
                    TimeoutMs = 1000
                },
                progress: (completed, total) => progress.Add((completed, total)),
                builtInOverride: (endpoint, name, type, token) => Task.FromResult(new ResolverQueryAttemptResult {
                    Target = "Cloudflare",
                    RequestFormat = DnsRequestFormat.DnsOverHttps,
                    Resolver = "1.1.1.1:443",
                    Response = new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        Answers = new[] {
                            new DnsAnswer {
                                Name = name,
                                Type = type,
                                DataRaw = "203.0.113.10"
                            }
                        }
                    },
                    Elapsed = TimeSpan.FromMilliseconds(7)
                }),
                cancellationToken: CancellationToken.None);

            Assert.Equal(4, results.Length);
            Assert.All(results, result => Assert.Equal("Cloudflare", result.Target));
            Assert.Equal((1, 4), progress[0]);
            Assert.Equal((4, 4), progress[progress.Count - 1]);
        }
    }
}
