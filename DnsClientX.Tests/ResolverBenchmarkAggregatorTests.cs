namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared benchmark attempt aggregation logic.
    /// </summary>
    public class ResolverBenchmarkAggregatorTests {
        /// <summary>
        /// Ensures successful attempts aggregate to latency statistics and distinct answer counts.
        /// </summary>
        [Fact]
        public void Aggregate_SuccessfulAttempts_ProducesExpectedCandidateSummary() {
            ResolverBenchmarkCandidate candidate = ResolverBenchmarkAggregator.Aggregate(
                "Cloudflare",
                new[] {
                    new ResolverBenchmarkAttemptObservation {
                        Resolver = "1.1.1.1:53",
                        Transport = "Doh",
                        ElapsedMs = 9,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverBenchmarkAttemptObservation {
                        Resolver = "1.1.1.1:53",
                        Transport = "Doh",
                        ElapsedMs = 7,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverBenchmarkAttemptObservation {
                        Resolver = "1.1.1.1:53",
                        Transport = "Doh",
                        ElapsedMs = 11,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|198.51.100.20"
                    }
                });

            Assert.Equal("Cloudflare", candidate.Target);
            Assert.Equal("1.1.1.1:53", candidate.Resolver);
            Assert.Equal("Doh", candidate.Transport);
            Assert.Equal(3, candidate.TotalQueries);
            Assert.Equal(3, candidate.SuccessCount);
            Assert.Equal(0, candidate.FailureCount);
            Assert.Equal(100, candidate.SuccessPercent);
            Assert.Equal(9, candidate.AverageMs);
            Assert.Equal(7, candidate.MinMs);
            Assert.Equal(11, candidate.MaxMs);
            Assert.Equal(2, candidate.DistinctAnswerSets);
        }

        /// <summary>
        /// Ensures failed attempts aggregate to a no-success candidate summary.
        /// </summary>
        [Fact]
        public void Aggregate_FailedAttempts_ProducesNoSuccessSummary() {
            ResolverBenchmarkCandidate candidate = ResolverBenchmarkAggregator.Aggregate(
                "Google",
                new[] {
                    new ResolverBenchmarkAttemptObservation {
                        Resolver = "none",
                        Transport = "none",
                        ElapsedMs = 50,
                        Succeeded = false
                    },
                    new ResolverBenchmarkAttemptObservation {
                        Resolver = "none",
                        Transport = "none",
                        ElapsedMs = 75,
                        Succeeded = false
                    }
                });

            Assert.Equal(2, candidate.TotalQueries);
            Assert.Equal(0, candidate.SuccessCount);
            Assert.Equal(2, candidate.FailureCount);
            Assert.Equal(0, candidate.SuccessPercent);
            Assert.Equal("none", candidate.Resolver);
            Assert.Equal("none", candidate.Transport);
            Assert.Equal(0, candidate.AverageMs);
            Assert.Equal(0, candidate.DistinctAnswerSets);
        }
    }
}
