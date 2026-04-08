using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared probe report construction.
    /// </summary>
    public class ResolverProbeReportBuilderTests {
        /// <summary>
        /// Ensures probe reports include ranked results, summary fields, and variant details.
        /// </summary>
        [Fact]
        public void Build_ProducesSummaryAndVariants() {
            ResolverProbeReport report = ResolverProbeReportBuilder.Build(
                new[] {
                    CreateAttempt("udp@1.1.1.1:53", DnsRequestFormat.DnsOverUDP, "1.1.1.1:53", true, 5, "example.com", "203.0.113.10"),
                    CreateAttempt("tcp@1.1.1.1:53", DnsRequestFormat.DnsOverTCP, "1.1.1.1:53", true, 8, "example.com", "203.0.113.10"),
                    CreateAttempt("udp@9.9.9.9:53", DnsRequestFormat.DnsOverUDP, "9.9.9.9:53", true, 12, "example.com", "198.51.100.20")
                },
                "example.com",
                DnsRecordType.A,
                2000,
                new ResolverProbePolicy());

            Assert.Equal(3, report.Results.Length);
            Assert.Equal(3, report.Summary.CandidateCount);
            Assert.Equal(2, report.Summary.DistinctAnswerSets);
            Assert.Equal(67, report.Summary.ConsensusPercent);
            Assert.Single(report.Summary.TransportCoverage);
            Assert.Equal(2, report.Summary.AnswerVariants.Length);
            Assert.Contains("udp@9.9.9.9:53", report.Summary.MismatchedTargets);
            Assert.Equal("udp@1.1.1.1:53", report.Summary.FastestSuccessTarget);
        }

        /// <summary>
        /// Ensures probe summaries surface runtime capability warnings for unsupported transports.
        /// </summary>
        [Fact]
        public void Build_TracksUnsupportedTransportWarnings() {
            ResolverProbeReport report = ResolverProbeReportBuilder.Build(
                new[] {
                    CreateAttempt("doh3@dns.quad9.net:443", DnsRequestFormat.DnsOverHttp3, "dns.quad9.net:443", false, 1, "example.com", "203.0.113.10"),
                    CreateAttempt("udp@1.1.1.1:53", DnsRequestFormat.DnsOverUDP, "1.1.1.1:53", true, 5, "example.com", "203.0.113.10")
                },
                "example.com",
                DnsRecordType.A,
                2000,
                new ResolverProbePolicy());

            Assert.Equal(DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttp3) ? 0 : 1, report.Summary.RuntimeUnsupportedCandidateCount);
            if (!DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttp3)) {
                Assert.Contains(report.Summary.RuntimeCapabilityWarnings, warning => warning.Contains("doh3@dns.quad9.net:443", StringComparison.Ordinal));
            }
        }

        private static ResolverQueryAttemptResult CreateAttempt(string target, DnsRequestFormat requestFormat, string resolver, bool succeeded, int elapsedMs, string name, string data) {
            return new ResolverQueryAttemptResult {
                Target = target,
                RequestFormat = requestFormat,
                Resolver = resolver,
                Response = succeeded
                    ? new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        Answers = new[] {
                            new DnsAnswer {
                                Name = name,
                                Type = DnsRecordType.A,
                                DataRaw = data
                            }
                        }
                    }
                    : null,
                Elapsed = TimeSpan.FromMilliseconds(elapsedMs),
                Error = succeeded ? null : "failure"
            };
        }
    }
}
