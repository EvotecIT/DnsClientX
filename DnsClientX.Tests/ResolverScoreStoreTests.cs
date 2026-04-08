using System;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests persisted resolver score snapshot storage helpers.
    /// </summary>
    public class ResolverScoreStoreTests {
        /// <summary>
        /// Ensures resolver score snapshots can be saved and loaded without losing recommendation data.
        /// </summary>
        [Fact]
        public void ResolverScoreStore_SaveAndLoad_RoundTripsSnapshot() {
            string path = Path.GetTempFileName();

            try {
                var snapshot = new ResolverScoreSnapshot {
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Benchmark,
                        RecommendationAvailable = true,
                        RecommendedTarget = "Cloudflare",
                        RecommendedResolver = "1.1.1.1:53",
                        RecommendedTransport = "Doh",
                        RecommendedAverageMs = 7
                    },
                    Results = new[] {
                        new ResolverScoreEntry {
                            Target = "Cloudflare",
                            Resolver = "1.1.1.1:53",
                            Transport = "Doh",
                            TotalQueries = 3,
                            SuccessCount = 3,
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
                };

                ResolverScoreStore.Save(path, snapshot);
                ResolverScoreSnapshot loaded = ResolverScoreStore.Load(path);

                Assert.Equal(ResolverScoreMode.Benchmark, loaded.Summary.Mode);
                Assert.True(loaded.Summary.RecommendationAvailable);
                Assert.Equal("Cloudflare", loaded.Summary.RecommendedTarget);
                Assert.Single(loaded.Results);
                Assert.Equal("Cloudflare", loaded.Results[0].Target);
                Assert.True(loaded.Results[0].IsRecommended);
            } finally {
                File.Delete(path);
            }
        }
    }
}
