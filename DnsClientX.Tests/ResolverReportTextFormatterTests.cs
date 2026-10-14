using System;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared report text formatting helpers.
    /// </summary>
    public class ResolverReportTextFormatterTests {
        /// <summary>Reordered rows with colliding labels still describe the selected candidate's latency.</summary>
        [Fact]
        public void BenchmarkBestUsesCandidateIdentity() {
            var rows = new[] {
                new ResolverBenchmarkReportResult { Target = "same", CandidateId = "first", SuccessCount = 1, SuccessPercent = 100, AverageMs = 42 },
                new ResolverBenchmarkReportResult { Target = "same", CandidateId = "second", SuccessCount = 1, SuccessPercent = 100, AverageMs = 7 }
            };
            var summary = new ResolverBenchmarkReportSummary {
                RecommendationAvailable = true, RecommendedTarget = "same", RecommendedCandidateId = "second"
            };
            Assert.Equal("same in 7 ms average (100% success) (candidate: second)", ResolverReportTextFormatter.DescribeBenchmarkBest(rows, summary, FormatDuration));
            Assert.Contains("(candidate: second)", ResolverReportTextFormatter.BuildBenchmarkRankedLine(rows[1], 1, FormatDuration));
            Assert.Contains("same (candidate: first)", ResolverReportTextFormatter.BuildBenchmarkResultLines(rows[0], FormatDuration)[0]);
            Assert.Contains("best_candidate_id=second", ResolverReportTextFormatter.BuildBenchmarkSummaryLine(summary, 0));
        }

        /// <summary>Same-label probe responders remain identifiable in every text summary and result surface.</summary>
        [Fact]
        public void ProbeTextPreservesCandidateIdentity() {
            const string first = "resolver-v1:abc";
            const string second = "resolver-v1:def";
            var summary = new ResolverProbeReportSummary {
                FastestSuccessTarget = "same", FastestSuccessCandidateId = first,
                FastestConsensusTarget = "same", FastestConsensusCandidateId = second,
                RecommendationAvailable = true, RecommendedTarget = "same", RecommendedCandidateId = second,
                MismatchedTargets = new[] { "same" }, MismatchedCandidateIds = new[] { first },
                AnswerVariants = new[] { new ResolverProbeAnswerVariant {
                    AnswerSet = "example.com A 192.0.2.1", Targets = new[] { "same", "same" }, CandidateIds = new[] { first, second }
                } }
            };
            Assert.Contains($"(candidate: {first})", ResolverReportTextFormatter.DescribeProbeFastestSuccess(summary, FormatDuration));
            Assert.Contains($"(candidate: {second})", ResolverReportTextFormatter.DescribeProbeFastestConsensus(summary, FormatDuration));
            Assert.Contains($"(candidate: {second})", ResolverReportTextFormatter.DescribeProbeRecommended(summary, FormatDuration));
            Assert.Equal($"same (candidate: {first})", ResolverReportTextFormatter.DescribeProbeMismatchedTargets(summary));
            Assert.Contains($"same (candidate: {first}), same (candidate: {second})", ResolverReportTextFormatter.DescribeProbeAnswerVariants(summary));
            Assert.Contains(first, ResolverReportTextFormatter.BuildProbeResultLines(new ResolverQueryAttemptResult { Target = "same", CandidateId = first }, FormatDuration)[0]);
            Assert.Contains(second, ResolverReportTextFormatter.BuildProbeResultLines(new ResolverProbeReportResult { Target = "same", CandidateId = second }, FormatDuration)[0]);
            string machine = ResolverReportTextFormatter.BuildProbeSummaryLine(summary, 0);
            Assert.Contains($"fastest_success_candidate_id={Uri.EscapeDataString(first)} ", machine);
            Assert.Contains($"fastest_consensus_candidate_id={Uri.EscapeDataString(second)} ", machine);
            Assert.Contains($"recommended_candidate_id={Uri.EscapeDataString(second)} ", machine);
        }

        /// <summary>Machine IDs round-trip exactly and cannot inject fields through whitespace or control characters.</summary>
        [Fact]
        public void SummaryCandidateIdentityIsPercentEncoded() {
            const string identity = "resolver-v1:abc result=pass\nnext";
            string encoded = Uri.EscapeDataString(identity);
            string benchmark = ResolverReportTextFormatter.BuildBenchmarkSummaryLine(new ResolverBenchmarkReportSummary { RecommendedCandidateId = identity }, 1);
            string probe = ResolverReportTextFormatter.BuildProbeSummaryLine(new ResolverProbeReportSummary { RecommendedCandidateId = identity }, 1);
            Assert.Contains($"best_candidate_id={encoded} ", benchmark);
            Assert.Contains($"recommended_candidate_id={encoded} ", probe);
            Assert.DoesNotContain(" result=pass", benchmark);
            Assert.DoesNotContain("\n", probe);
            Assert.Equal(identity, Uri.UnescapeDataString(encoded));
        }

        /// <summary>Probe consensus text cannot emit a control sequence from an answer set.</summary>
        [Fact]
        public void ProbeAnswerVariantsEscapeTerminalControls() {
            var summary = new ResolverProbeReportSummary {
                AnswerVariants = new[] {
                    new ResolverProbeAnswerVariant {
                        AnswerSet = "example.com TXT red\u001b[31m",
                        Targets = new[] { "udp@1.1.1.1:53" }
                    }
                }
            };

            string text = ResolverReportTextFormatter.DescribeProbeAnswerVariants(summary);

            Assert.Equal(-1, text.IndexOf('\u001b'));
            Assert.Contains("red\\u001B[31m", text, StringComparison.Ordinal);
            Assert.Equal("example.com TXT red\u001b[31m", summary.AnswerVariants[0].AnswerSet);
        }

        /// <summary>
        /// Ensures probe summary helpers preserve the expected human-readable wording.
        /// </summary>
        [Fact]
        public void ProbeFormatting_ProducesExpectedStrings() {
            var summary = new ResolverProbeReportSummary {
                FastestSuccessTarget = "udp@1.1.1.1:53",
                FastestSuccessTransport = "Udp",
                FastestSuccessMs = 5,
                FastestConsensusTarget = "tcp@1.1.1.1:53",
                FastestConsensusTransport = "Tcp",
                FastestConsensusMs = 8,
                ConsensusCount = 2,
                ConsensusTotal = 3,
                MismatchedTargets = new[] { "udp@9.9.9.9:53" },
                AnswerVariants = new[] {
                    new ResolverProbeAnswerVariant {
                        AnswerSet = "example.com A 203.0.113.10",
                        Targets = new[] { "udp@1.1.1.1:53", "tcp@1.1.1.1:53" }
                    }
                },
                TransportCoverage = new[] {
                    new ResolverProbeTransportCoverage {
                        Transport = "Udp",
                        SuccessfulCount = 1,
                        TotalCount = 2
                    }
                },
                RecommendationAvailable = true,
                RecommendedTarget = "udp@1.1.1.1:53",
                RecommendedTransport = "Udp",
                RecommendedResolver = "1.1.1.1:53",
                RecommendedAverageMs = 5,
                RecommendationStatus = "selected",
                RecommendationSource = "unanimous agreement",
                RecommendationReason = "none",
                PolicyPassed = true,
                PolicyReason = "none",
                SuccessfulCandidates = 2,
                CandidateCount = 3,
                SuccessPercent = 67,
                DistinctAnswerSets = 1
            };

            Assert.Equal("udp@1.1.1.1:53 in 5 ms via Udp", ResolverReportTextFormatter.DescribeProbeFastestSuccess(summary, FormatDuration));
            Assert.Equal("tcp@1.1.1.1:53 in 8 ms via Tcp", ResolverReportTextFormatter.DescribeProbeFastestConsensus(summary, FormatDuration));
            Assert.Equal("Udp 1/2", ResolverReportTextFormatter.DescribeProbeTransportCoverage(summary));
            Assert.Equal("2/3 successful probes agree", ResolverReportTextFormatter.DescribeProbeAnswerConsensus(summary));
            Assert.Equal("udp@9.9.9.9:53", ResolverReportTextFormatter.DescribeProbeMismatchedTargets(summary));
            Assert.Contains("[1] example.com A 203.0.113.10 <- udp@1.1.1.1:53, tcp@1.1.1.1:53", ResolverReportTextFormatter.DescribeProbeAnswerVariants(summary));
            Assert.Equal("udp@1.1.1.1:53 in 5 ms via Udp", ResolverReportTextFormatter.DescribeProbeRecommended(summary, FormatDuration));
            Assert.Equal("all requested transports supported on this runtime", ResolverReportTextFormatter.DescribeProbeRuntimeCapabilities(summary));
            Assert.Contains("recommended_target=udp_1_1_1_1_53", ResolverReportTextFormatter.BuildProbeSummaryLine(summary, 0));
        }

        /// <summary>
        /// Ensures benchmark summary helpers preserve the expected human-readable wording.
        /// </summary>
        [Fact]
        public void BenchmarkFormatting_ProducesExpectedStrings() {
            var summary = new ResolverBenchmarkReportSummary {
                CandidateCount = 2,
                SuccessfulCandidates = 2,
                OverallSuccessCount = 4,
                OverallQueryCount = 4,
                OverallSuccessPercent = 100,
                TimeoutMs = 2000,
                MaxConcurrency = 4,
                PolicyPassed = true,
                PolicyReason = "none",
                RecommendationAvailable = true,
                RecommendedTarget = "Cloudflare",
                RecommendedResolver = "1.1.1.1:443",
                RecommendedTransport = "Doh",
                RecommendedAverageMs = 7
            };
            var results = new[] {
                new ResolverBenchmarkReportResult {
                    Target = "Cloudflare",
                    Resolver = "1.1.1.1:443",
                    SuccessCount = 2,
                    TotalQueries = 2,
                    SuccessPercent = 100,
                    AverageMs = 7
                }
            };

            Assert.Equal("  Ranked 1: Cloudflare avg 7 ms, success 100% (2/2), resolver 1.1.1.1:443", ResolverReportTextFormatter.BuildBenchmarkRankedLine(results[0], 1, FormatDuration));
            Assert.Equal("Cloudflare in 7 ms average (100% success)", ResolverReportTextFormatter.DescribeBenchmarkBest(results, summary, FormatDuration));
            Assert.Equal("all requested transports supported on this runtime", ResolverReportTextFormatter.DescribeBenchmarkRuntimeCapabilities(summary));
            Assert.Contains("best_target=cloudflare", ResolverReportTextFormatter.BuildBenchmarkSummaryLine(summary, 0));
        }

        /// <summary>
        /// Ensures per-result CLI block formatting is preserved in shared helpers.
        /// </summary>
        [Fact]
        public void ResultBlockFormatting_ProducesExpectedLines() {
            ResolverQueryAttemptResult probe = new ResolverQueryAttemptResult {
                Target = "udp@1.1.1.1:53",
                RequestFormat = DnsRequestFormat.DnsOverUDP,
                Resolver = "1.1.1.1:53",
                Response = new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = Transport.Udp
                },
                Elapsed = TimeSpan.FromMilliseconds(5)
            };

            string[] probeLines = ResolverReportTextFormatter.BuildProbeResultLines(probe, FormatDuration);
            Assert.Equal("  [OK] udp@1.1.1.1:53 via DnsOverUDP", probeLines[0]);
            Assert.Contains("      Transport: Udp", probeLines);

            ResolverQueryAttemptResult failedProbe = new ResolverQueryAttemptResult {
                Target = "tcp@9.9.9.9:53",
                RequestFormat = DnsRequestFormat.DnsOverTCP,
                Resolver = "9.9.9.9:53",
                Elapsed = TimeSpan.FromMilliseconds(20),
                Error = "timeout"
            };

            string[] failedProbeLines = ResolverReportTextFormatter.BuildProbeResultLines(failedProbe, FormatDuration);
            Assert.Contains("      Transport: Tcp", failedProbeLines);
            Assert.Contains("      Error: timeout", failedProbeLines);

            string[] probeReportLines = ResolverReportTextFormatter.BuildProbeResultLines(new ResolverProbeReportResult {
                Target = "doh@example.test/dns-query",
                Resolver = "203.0.113.10:443",
                RequestFormat = DnsRequestFormat.DnsOverHttps,
                Transport = "Doh",
                Status = "NoError",
                Error = "none",
                ElapsedMs = 11,
                AnswerCount = 1,
                Succeeded = true
            }, FormatDuration);
            Assert.Contains("      Transport: Doh", probeReportLines);
            Assert.Contains("      Answers: 1", probeReportLines);

            string[] benchmarkLines = ResolverReportTextFormatter.BuildBenchmarkResultLines(new ResolverBenchmarkReportResult {
                Target = "Cloudflare",
                Resolver = "1.1.1.1:443",
                Transport = "Doh",
                TotalQueries = 3,
                SuccessCount = 2,
                SuccessPercent = 67,
                AverageMs = 8,
                MinMs = 7,
                MaxMs = 10,
                DistinctAnswerSets = 1
            }, FormatDuration);

            Assert.Equal("  [OK] Cloudflare", benchmarkLines[0]);
            Assert.Contains("      Success rate: 67% (2/3)", benchmarkLines);
        }

        /// <summary>
        /// Ensures single-operation explain and trace formatting is preserved in shared helpers.
        /// </summary>
        [Fact]
        public void SingleOperationFormatting_ProducesExpectedLines() {
            ResolverSingleOperationResult result = new ResolverSingleOperationResult {
                Response = new DnsResponse {
                    Status = DnsResponseCode.NoError,
                    UsedTransport = Transport.Udp,
                    RetryCount = 1,
                    Questions = new[] {
                        new DnsQuestion {
                            Name = "example.com",
                            Type = DnsRecordType.A,
                            RequestFormat = DnsRequestFormat.DnsOverUDP
                        }
                    },
                    Answers = new[] {
                        new DnsAnswer {
                            Name = "example.com",
                            Type = DnsRecordType.A,
                            DataRaw = "203.0.113.10"
                        }
                    }
                },
                Elapsed = TimeSpan.FromMilliseconds(5),
                SelectionStrategy = DnsSelectionStrategy.First,
                RequestFormat = DnsRequestFormat.DnsOverUDP,
                CacheEnabled = false,
                ConfiguredResolverHost = "127.0.0.1",
                ConfiguredResolverPort = 53,
                AuditTrail = new[] {
                    new AuditEntry("example.com", DnsRecordType.A) {
                        AttemptNumber = 1,
                        ResolverHost = "127.0.0.1",
                        ResolverPort = 53,
                        RequestFormat = DnsRequestFormat.DnsOverUDP,
                        UsedTransport = Transport.Udp,
                        Duration = TimeSpan.FromMilliseconds(2),
                        RetryReason = "transient response",
                        Response = new DnsResponse {
                            Status = DnsResponseCode.ServerFailure
                        }
                    },
                    new AuditEntry("example.com", DnsRecordType.A) {
                        AttemptNumber = 2,
                        ResolverHost = "127.0.0.1",
                        ResolverPort = 53,
                        RequestFormat = DnsRequestFormat.DnsOverUDP,
                        UsedTransport = Transport.Udp,
                        Duration = TimeSpan.FromMilliseconds(3),
                        Response = new DnsResponse {
                            Status = DnsResponseCode.NoError
                        }
                    }
                }
            };

            string[] explainLines = ResolverReportTextFormatter.BuildSingleOperationExplainLines(
                result,
                "query",
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                FormatDuration);

            Assert.Contains("Explain:", explainLines);
            Assert.Contains("  Resolver: 127.0.0.1:53", explainLines);
            Assert.Contains("  Runtime transport support: supported", explainLines);
            Assert.Contains("  Retry reasons: transient response", explainLines);

            string[] traceLines = ResolverReportTextFormatter.BuildSingleOperationTraceLines(result, FormatDuration);
            Assert.Contains("Trace:", traceLines);
            Assert.Contains("  Question: example.com A via DnsOverUDP", traceLines);
            Assert.Contains("  Attempt 1: example.com A via DnsOverUDP/Udp to 127.0.0.1:53 => ServerFailure in 2 ms (network, exception: None, retry: transient response)", traceLines);
            Assert.Contains("  Attempt 2: example.com A via DnsOverUDP/Udp to 127.0.0.1:53 => NoError in 3 ms (network, exception: None)", traceLines);
        }

#if NET472
        /// <summary>
        /// Ensures unsupported modern transports are called out explicitly in explain output on older frameworks.
        /// </summary>
        [Fact]
        public void SingleOperationFormatting_ReportsUnsupportedModernTransport() {
            ResolverSingleOperationResult result = new ResolverSingleOperationResult {
                Response = new DnsResponse {
                    Status = DnsResponseCode.NotImplemented,
                    Questions = new[] {
                        new DnsQuestion {
                            Name = "example.com",
                            Type = DnsRecordType.A,
                            RequestFormat = DnsRequestFormat.DnsOverHttp3
                        }
                    }
                },
                RequestFormat = DnsRequestFormat.DnsOverHttp3,
                ConfiguredResolverHost = "dns.quad9.net",
                ConfiguredResolverPort = 443
            };

            string[] explainLines = ResolverReportTextFormatter.BuildSingleOperationExplainLines(
                result,
                "query",
                "example.com",
                DnsRecordType.A,
                requestDnsSec: false,
                validateDnsSec: false,
                FormatDuration);

            Assert.Contains("  Runtime transport support: DNS over HTTP/3 is supported in the core package on net8+ and is unavailable on this runtime.", explainLines);
        }
#endif

        private static string FormatDuration(TimeSpan duration) {
            return $"{(int)Math.Round(duration.TotalMilliseconds, MidpointRounding.AwayFromZero)} ms";
        }
    }
}
