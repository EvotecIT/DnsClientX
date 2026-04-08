namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver probe scoring logic.
    /// </summary>
    public class ResolverProbeScorerTests {
        /// <summary>
        /// Ensures unanimous successful probe observations produce a recommendation and snapshot-friendly ranking.
        /// </summary>
        [Fact]
        public void Evaluate_UnanimousSuccess_ProducesRecommendation() {
            var evaluation = ResolverProbeScorer.Evaluate(
                new[] {
                    new ResolverProbeObservation {
                        Target = "udp@1.1.1.1:53",
                        Resolver = "1.1.1.1:53",
                        Transport = "Udp",
                        ElapsedMs = 5,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverProbeObservation {
                        Target = "tcp@1.1.1.1:53",
                        Resolver = "1.1.1.1:53",
                        Transport = "Tcp",
                        ElapsedMs = 9,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    }
                },
                new ResolverProbePolicy());

            Assert.True(evaluation.PolicyPassed);
            Assert.True(evaluation.RecommendationAvailable);
            Assert.Equal("udp@1.1.1.1:53", evaluation.RecommendedTarget);
            Assert.Equal("unanimous agreement", evaluation.RecommendationSource);
            Assert.Equal(100, evaluation.ConsensusPercent);
            Assert.Equal(2, evaluation.Results.Length);
            Assert.True(evaluation.Results[0].IsRecommended);
        }

        /// <summary>
        /// Ensures disagreement without a consensus policy does not emit a recommendation.
        /// </summary>
        [Fact]
        public void Evaluate_MajorityWithoutConsensusPolicy_BlocksRecommendation() {
            var evaluation = ResolverProbeScorer.Evaluate(
                new[] {
                    new ResolverProbeObservation {
                        Target = "udp@1.1.1.1:53",
                        Resolver = "1.1.1.1:53",
                        Transport = "Udp",
                        ElapsedMs = 5,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverProbeObservation {
                        Target = "udp@1.1.1.2:53",
                        Resolver = "1.1.1.2:53",
                        Transport = "Udp",
                        ElapsedMs = 7,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverProbeObservation {
                        Target = "tcp@9.9.9.9:53",
                        Resolver = "9.9.9.9:53",
                        Transport = "Tcp",
                        ElapsedMs = 8,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|198.51.100.20"
                    }
                },
                new ResolverProbePolicy());

            Assert.True(evaluation.PolicyPassed);
            Assert.False(evaluation.RecommendationAvailable);
            Assert.Equal("none", evaluation.RecommendedTarget);
            Assert.Equal("none", evaluation.RecommendationSource);
            Assert.Equal("consensus policy not enabled", evaluation.RecommendationReason);
            Assert.Equal(67, evaluation.ConsensusPercent);
        }

        /// <summary>
        /// Ensures consensus policy failures are surfaced consistently.
        /// </summary>
        [Fact]
        public void Evaluate_MinConsensusPolicy_FailsWhenThresholdNotMet() {
            var evaluation = ResolverProbeScorer.Evaluate(
                new[] {
                    new ResolverProbeObservation {
                        Target = "udp@1.1.1.1:53",
                        Resolver = "1.1.1.1:53",
                        Transport = "Udp",
                        ElapsedMs = 5,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverProbeObservation {
                        Target = "udp@1.1.1.2:53",
                        Resolver = "1.1.1.2:53",
                        Transport = "Udp",
                        ElapsedMs = 7,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|203.0.113.10"
                    },
                    new ResolverProbeObservation {
                        Target = "tcp@9.9.9.9:53",
                        Resolver = "9.9.9.9:53",
                        Transport = "Tcp",
                        ElapsedMs = 8,
                        Succeeded = true,
                        AnswerSignature = "example.com|A|198.51.100.20"
                    }
                },
                new ResolverProbePolicy {
                    MinConsensusPercent = 80
                });

            Assert.False(evaluation.PolicyPassed);
            Assert.Equal("consensus 67% below required 80%", evaluation.PolicyReason);
            Assert.False(evaluation.RecommendationAvailable);
            Assert.Equal("blocked by policy", evaluation.RecommendationStatus);
            Assert.Equal("policy failed", evaluation.RecommendationReason);
        }
    }
}
