using System;
using System.IO;
using System.Threading.Tasks;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests shared resolver execution target resolution.
    /// </summary>
    public class ResolverExecutionTargetResolverTests {
        /// <summary>
        /// Ensures built-in probe profiles expand through the shared target resolver.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_ExpandsProbeProfile() {
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(new ResolverExecutionTargetSource {
                ProbeProfile = DnsEndpoint.Cloudflare
            });

            Assert.NotEmpty(targets);
            Assert.All(targets, target => Assert.NotNull(target.BuiltInEndpoint));
        }

        /// <summary>
        /// Ensures explicit inline endpoints are parsed and normalized through the shared target resolver.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_ParsesExplicitEndpoints() {
            ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(new ResolverExecutionTargetSource {
                ResolverEndpoints = new[] { "tcp@1.1.1.1:53", "udp@9.9.9.9:53" }
            });

            Assert.Equal(2, targets.Length);
            Assert.All(targets, target => Assert.NotNull(target.ExplicitEndpoint));
        }

        /// <summary>
        /// Ensures saved recommendations resolve through the shared target resolver.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_LoadsRecommendedSelection() {
            string path = Path.GetTempFileName();

            try {
                ResolverScoreStore.Save(path, new ResolverScoreSnapshot {
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Benchmark,
                        RecommendationAvailable = true,
                        RecommendedTarget = "Cloudflare",
                        RecommendedResolver = "1.1.1.1:443",
                        RecommendedTransport = "Doh",
                        RecommendedAverageMs = 7
                    },
                    Results = new[] {
                        new ResolverScoreEntry {
                            Target = "Cloudflare",
                            Resolver = "1.1.1.1:443",
                            Transport = "Doh",
                            TotalQueries = 2,
                            SuccessCount = 2,
                            FailureCount = 0,
                            SuccessPercent = 100,
                            AverageMs = 7,
                            MinMs = 6,
                            MaxMs = 8,
                            DistinctAnswerSets = 1,
                            Rank = 1,
                            IsBest = true,
                            IsRecommended = true
                        }
                    }
                });

                ResolverExecutionTarget[] targets = await ResolverExecutionTargetResolver.ResolveAsync(new ResolverExecutionTargetSource {
                    ResolverSelectionPath = path
                });

                Assert.Single(targets);
                Assert.Equal(DnsEndpoint.Cloudflare, targets[0].BuiltInEndpoint);
            } finally {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Ensures single-target resolution returns the only effective built-in target.
        /// </summary>
        [Fact]
        public async Task ResolveSingleAsync_ReturnsSingleBuiltInTarget() {
            ResolverExecutionTarget target = await ResolverExecutionTargetResolver.ResolveSingleAsync(new ResolverExecutionTargetSource {
                BuiltInEndpoints = new[] { DnsEndpoint.Cloudflare }
            });

            Assert.Equal(DnsEndpoint.Cloudflare, target.BuiltInEndpoint);
            Assert.Null(target.ExplicitEndpoint);
        }

        /// <summary>
        /// Ensures invalid mixed target sources are rejected.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_RejectsMixedSources() {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ResolverExecutionTargetResolver.ResolveAsync(new ResolverExecutionTargetSource {
                ProbeProfile = DnsEndpoint.Cloudflare,
                BuiltInEndpoints = new[] { DnsEndpoint.Google }
            }));
        }

        /// <summary>
        /// Ensures single-target resolution rejects sources that expand to more than one target.
        /// </summary>
        [Fact]
        public async Task ResolveSingleAsync_RejectsMultipleTargets() {
            await Assert.ThrowsAsync<InvalidOperationException>(() => ResolverExecutionTargetResolver.ResolveSingleAsync(new ResolverExecutionTargetSource {
                BuiltInEndpoints = new[] { DnsEndpoint.Cloudflare, DnsEndpoint.Google }
            }));
        }

        /// <summary>
        /// Ensures snapshots that recommend the custom built-in endpoint fail with a clear reuse error.
        /// </summary>
        [Fact]
        public async Task ResolveAsync_CustomSnapshotSelection_FailsClearly() {
            string path = Path.GetTempFileName();

            try {
                ResolverScoreStore.Save(path, new ResolverScoreSnapshot {
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Probe,
                        RecommendationAvailable = true,
                        RecommendedTarget = "Custom",
                        RecommendedResolver = "custom.example:443",
                        RecommendedTransport = "Doh",
                        RecommendedAverageMs = 5
                    }
                });

                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ResolverExecutionTargetResolver.ResolveAsync(new ResolverExecutionTargetSource {
                    ResolverSelectionPath = path
                }));

                Assert.Contains("cannot be reused automatically", exception.Message, StringComparison.OrdinalIgnoreCase);
            } finally {
                File.Delete(path);
            }
        }
    }
}
