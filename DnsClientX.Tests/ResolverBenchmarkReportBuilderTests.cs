using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared benchmark report construction.
    /// </summary>
    public class ResolverBenchmarkReportBuilderTests {
        /// <summary>
        /// Ensures benchmark reports include ranked results, summary fields, and snapshot metadata.
        /// </summary>
        [Fact]
        public void Build_ProducesRankedBenchmarkReport() {
            ResolverBenchmarkReport report = ResolverBenchmarkReportBuilder.Build(
                new[] {
                    CreateAttempt("Cloudflare", "1.1.1.1:443", "Doh", 7, true),
                    CreateAttempt("Cloudflare", "1.1.1.1:443", "Doh", 9, true),
                    CreateAttempt("Google", "8.8.8.8:443", "Doh", 15, true),
                    CreateAttempt("Google", "8.8.8.8:443", "Doh", 30, false)
                },
                new[] { "example.com" },
                new[] { DnsRecordType.A },
                2,
                4,
                2000,
                new ResolverBenchmarkPolicy());

            Assert.Equal(2, report.Results.Length);
            Assert.Equal("Cloudflare", report.Results[0].Target);
            Assert.True(report.Results[0].IsBest);
            Assert.Equal(2, report.Summary.CandidateCount);
            Assert.Equal(3, report.Summary.OverallSuccessCount);
            Assert.Equal(4, report.Snapshot.Summary.MaxConcurrency);
        }

        /// <summary>
        /// Ensures benchmark summaries surface runtime capability warnings for unsupported transports.
        /// </summary>
        [Fact]
        public void Build_TracksUnsupportedTransportWarnings() {
            ResolverBenchmarkReport report = ResolverBenchmarkReportBuilder.Build(
                new[] {
                    CreateAttempt("Quad9DoH3", "dns.quad9.net:443", "Http3", 1, false, DnsRequestFormat.DnsOverHttp3),
                    CreateAttempt("Cloudflare", "1.1.1.1:443", "Doh", 7, true, DnsRequestFormat.DnsOverHttps)
                },
                new[] { "example.com" },
                new[] { DnsRecordType.A },
                1,
                4,
                2000,
                new ResolverBenchmarkPolicy());

            Assert.Equal(DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttp3) ? 0 : 1, report.Summary.RuntimeUnsupportedCandidateCount);
            Assert.Equal(report.Summary.RuntimeUnsupportedCandidateCount, report.Snapshot.Summary.RuntimeUnsupportedCandidateCount);
            Assert.Equal(report.Summary.RuntimeCapabilityWarnings, report.Snapshot.Summary.RuntimeCapabilityWarnings);
            if (!DnsTransportCapabilities.Supports(DnsRequestFormat.DnsOverHttp3)) {
                Assert.Contains(report.Summary.RuntimeCapabilityWarnings, warning => warning.Contains("Quad9DoH3", StringComparison.Ordinal));
            }
        }

        private static ResolverQueryAttemptResult CreateAttempt(string target, string resolver, string transport, int elapsedMs, bool succeeded, DnsRequestFormat requestFormat = DnsRequestFormat.DnsOverHttps) {
            return new ResolverQueryAttemptResult {
                Target = target,
                RequestFormat = requestFormat,
                Resolver = resolver,
                Response = succeeded
                    ? new DnsResponse {
                        Status = DnsResponseCode.NoError,
                        Answers = new[] {
                            new DnsAnswer {
                                Name = "example.com",
                                Type = DnsRecordType.A,
                                DataRaw = "203.0.113.10"
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
