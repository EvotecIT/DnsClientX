using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver probe execution.
    /// </summary>
    public class ResolverProbeRunnerTests {
        /// <summary>
        /// Ensures probe execution uses normalized targets, override delegates, and progress reporting.
        /// </summary>
        [Fact]
        public async Task RunAsync_UsesOverridesAndReportsProgress() {
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
            ResolverQueryAttemptResult[] results = await ResolverProbeRunner.RunAsync(
                targets,
                "example.com",
                DnsRecordType.A,
                new ResolverQueryRunOptions {
                    TimeoutMs = 1000
                },
                progress: (completed, total) => progress.Add((completed, total)),
                builtInOverride: (endpoint, name, type, token) => Task.FromResult(CreateSuccess("System", DnsRequestFormat.DnsOverUDP, "system:53", "example.com|A|203.0.113.10")),
                explicitOverride: (endpoint, name, type, token) => Task.FromResult(CreateSuccess("tcp@1.1.1.1:53", DnsRequestFormat.DnsOverTCP, "1.1.1.1:53", "example.com|A|198.51.100.20")),
                cancellationToken: CancellationToken.None);

            Assert.Equal(2, results.Length);
            Assert.Equal("System", results[0].Target);
            Assert.Equal("tcp@1.1.1.1:53", results[1].Target);
            Assert.True(results[0].Succeeded);
            Assert.True(results[1].Succeeded);
            Assert.Equal(2, progress.Count);
            Assert.Equal((1, 2), progress[0]);
            Assert.Equal((2, 2), progress[1]);
        }

        private static ResolverQueryAttemptResult CreateSuccess(string target, DnsRequestFormat requestFormat, string resolver, string answerData) {
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
