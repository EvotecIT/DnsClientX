using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver probe workflow orchestration.
    /// </summary>
    public class ResolverProbeWorkflowTests {
        /// <summary>
        /// Ensures the probe workflow composes execution and report building into one shared entry point.
        /// </summary>
        [Theory]
        [InlineData(1000)]
        [InlineData(1500)]
        public async Task RunAsync_ComposesExecutionAndReportBuilding(int timeoutMs) {
            ResolverExecutionTarget[] targets = {
                new ResolverExecutionTarget {
                    DisplayName = "System",
                    BuiltInEndpoint = DnsEndpoint.System
                },
                new ResolverExecutionTarget {
                    DisplayName = "tcp@1.1.1.1:53",
                    ExplicitEndpoint = new DnsResolverEndpoint {
                        Transport = Transport.Tcp,
                        Host = "1.1.1.1",
                        Port = 53
                    }
                }
            };

            var progress = new List<(int Completed, int Total)>();
            ResolverProbeReport report = await ResolverProbeWorkflow.RunAsync(
                targets,
                "example.com",
                DnsRecordType.A,
                timeoutMs: timeoutMs,
                new ResolverQueryRunOptions {
                    TimeoutMs = timeoutMs
                },
                new ResolverProbePolicy {
                    MinSuccessCount = 1
                },
                progress: (completed, total) => progress.Add((completed, total)),
                builtInOverride: (endpoint, name, type, token) => Task.FromResult(CreateAttempt("System", DnsRequestFormat.DnsOverUDP, "system:53", "203.0.113.10")),
                explicitOverride: (endpoint, name, type, token) => Task.FromResult(CreateAttempt("tcp@1.1.1.1:53", DnsRequestFormat.DnsOverTCP, "1.1.1.1:53", "198.51.100.20")),
                cancellationToken: CancellationToken.None);

            Assert.Equal(2, report.Results.Length);
            Assert.All(report.Results, result => Assert.True(result.Succeeded));
            Assert.Equal(new[] { "System", "tcp@1.1.1.1:53" }, report.Snapshot.Answers.Select(answer => answer.Target));
            Assert.All(report.Snapshot.Answers, answer => Assert.Equal("NoError", answer.Status));
            Assert.Equal(2, progress.Count);
            Assert.Equal(2, report.Summary.CandidateCount);
            Assert.True(report.Evaluation.PolicyPassed);
            Assert.Equal("example.com", report.Summary.Name);
            Assert.Equal(timeoutMs, report.Summary.TimeoutMs);
            Assert.Equal((1, 2), progress[0]);
            Assert.Equal((2, 2), progress[1]);
        }

        private static ResolverQueryAttemptResult CreateAttempt(string target, DnsRequestFormat requestFormat, string resolver, string answerData) {
            return new ResolverQueryAttemptResult {
                Target = target,
                RequestFormat = requestFormat,
                Resolver = resolver,
                Response = new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    Answers = new[] {
                        new DnsAnswer {
                            Name = "example.com",
                            Type = DnsRecordType.A,
                            DataRaw = answerData
                        }
                    }
                },
                Elapsed = TimeSpan.FromMilliseconds(5)
            };
        }
    }
}
