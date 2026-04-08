namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver benchmark scoring logic.
    /// </summary>
    public class ResolverBenchmarkScorerTests {
        /// <summary>
        /// Ensures the best successful candidate is recommended when policy passes.
        /// </summary>
        [Fact]
        public void Evaluate_PassingPolicy_ProducesRecommendation() {
            var evaluation = ResolverBenchmarkScorer.Evaluate(
                new[] {
                    new ResolverBenchmarkCandidate {
                        Target = "Cloudflare",
                        Resolver = "1.1.1.1:53",
                        Transport = "Doh",
                        TotalQueries = 4,
                        SuccessCount = 4,
                        FailureCount = 0,
                        AverageMs = 7,
                        MinMs = 6,
                        MaxMs = 8,
                        DistinctAnswerSets = 1
                    },
                    new ResolverBenchmarkCandidate {
                        Target = "Google",
                        Resolver = "8.8.8.8:53",
                        Transport = "Doh",
                        TotalQueries = 4,
                        SuccessCount = 3,
                        FailureCount = 1,
                        AverageMs = 12,
                        MinMs = 10,
                        MaxMs = 15,
                        DistinctAnswerSets = 1
                    }
                },
                new ResolverBenchmarkPolicy {
                    MinSuccessPercent = 50,
                    MinSuccessfulCandidates = 1
                });

            Assert.True(evaluation.PolicyPassed);
            Assert.True(evaluation.RecommendationAvailable);
            Assert.Equal("Cloudflare", evaluation.RecommendedTarget);
            Assert.Equal("1.1.1.1:53", evaluation.RecommendedResolver);
            Assert.Equal(88, evaluation.OverallSuccessPercent);
            Assert.True(evaluation.Results[0].IsRecommended);
        }

        /// <summary>
        /// Ensures benchmark policy can fail when too few candidates succeed.
        /// </summary>
        [Fact]
        public void Evaluate_MinSuccessfulCandidates_FailsWhenTooFewCandidatesSucceed() {
            var evaluation = ResolverBenchmarkScorer.Evaluate(
                new[] {
                    new ResolverBenchmarkCandidate {
                        Target = "Cloudflare",
                        Resolver = "1.1.1.1:53",
                        Transport = "Doh",
                        TotalQueries = 2,
                        SuccessCount = 2,
                        FailureCount = 0,
                        AverageMs = 7,
                        MinMs = 6,
                        MaxMs = 8,
                        DistinctAnswerSets = 1
                    },
                    new ResolverBenchmarkCandidate {
                        Target = "Google",
                        Resolver = "none",
                        Transport = "none",
                        TotalQueries = 2,
                        SuccessCount = 0,
                        FailureCount = 2,
                        AverageMs = 0,
                        MinMs = 0,
                        MaxMs = 0,
                        DistinctAnswerSets = 0
                    }
                },
                new ResolverBenchmarkPolicy {
                    MinSuccessfulCandidates = 2
                });

            Assert.False(evaluation.PolicyPassed);
            Assert.Equal("successful candidates 1/2 below required count 2", evaluation.PolicyReason);
            Assert.False(evaluation.RecommendationAvailable);
        }

        /// <summary>
        /// Ensures benchmark policy can fail on overall success percentage.
        /// </summary>
        [Fact]
        public void Evaluate_MinSuccessPercent_FailsWhenOverallRateTooLow() {
            var evaluation = ResolverBenchmarkScorer.Evaluate(
                new[] {
                    new ResolverBenchmarkCandidate {
                        Target = "Cloudflare",
                        Resolver = "1.1.1.1:53",
                        Transport = "Doh",
                        TotalQueries = 4,
                        SuccessCount = 2,
                        FailureCount = 2,
                        AverageMs = 7,
                        MinMs = 6,
                        MaxMs = 8,
                        DistinctAnswerSets = 1
                    },
                    new ResolverBenchmarkCandidate {
                        Target = "Google",
                        Resolver = "8.8.8.8:53",
                        Transport = "Doh",
                        TotalQueries = 4,
                        SuccessCount = 2,
                        FailureCount = 2,
                        AverageMs = 9,
                        MinMs = 8,
                        MaxMs = 10,
                        DistinctAnswerSets = 1
                    }
                },
                new ResolverBenchmarkPolicy {
                    MinSuccessPercent = 75
                });

            Assert.False(evaluation.PolicyPassed);
            Assert.Equal("success rate 50% below required 75%", evaluation.PolicyReason);
            Assert.False(evaluation.RecommendationAvailable);
            Assert.Equal(50, evaluation.OverallSuccessPercent);
        }
    }
}
