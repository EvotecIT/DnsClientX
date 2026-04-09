using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver benchmark workflow orchestration.
    /// </summary>
    public class ResolverBenchmarkWorkflowTests {
        /// <summary>
        /// Ensures the benchmark workflow composes execution and report building into one shared entry point.
        /// </summary>
        [Fact]
        public async Task RunAsync_ComposesExecutionAndReportBuilding() {
            ResolverExecutionTarget[] targets = {
                new ResolverExecutionTarget {
                    DisplayName = "Cloudflare",
                    BuiltInEndpoint = DnsEndpoint.Cloudflare
                }
            };

            var progress = new List<(int Completed, int Total)>();
            ResolverBenchmarkReport report = await ResolverBenchmarkWorkflow.RunAsync(
                targets,
                new[] { "example.com", "example.net" },
                new[] { DnsRecordType.A },
                attemptsPerCombination: 2,
                maxConcurrency: 2,
                timeoutMs: 1200,
                new ResolverQueryRunOptions {
                    TimeoutMs = 1200
                },
                new ResolverBenchmarkPolicy {
                    MinSuccessfulCandidates = 1
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

            Assert.Single(report.Results);
            Assert.Equal(1, report.Summary.CandidateCount);
            Assert.True(report.Evaluation.PolicyPassed);
            Assert.Equal(1200, report.Summary.TimeoutMs);
            Assert.Equal((1, 4), progress[0]);
            Assert.Equal((4, 4), progress[progress.Count - 1]);
        }
    }
}
