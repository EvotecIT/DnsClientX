using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                        RecommendedAverageMs = 7,
                        RuntimeUnsupportedCandidateCount = 1,
                        RuntimeCapabilityWarnings = new[] {
                            "Quad9DoH3: DNS over HTTP/3 is not supported on this runtime."
                        }
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

                Assert.Equal(ResolverScoreSnapshot.CurrentSchemaVersion, loaded.SchemaVersion);
                Assert.Equal(ResolverScoreMode.Benchmark, loaded.Summary.Mode);
                Assert.True(loaded.Summary.RecommendationAvailable);
                Assert.Equal("Cloudflare", loaded.Summary.RecommendedTarget);
                Assert.Equal(1, loaded.Summary.RuntimeUnsupportedCandidateCount);
                Assert.Single(loaded.Summary.RuntimeCapabilityWarnings);
                Assert.Single(loaded.Results);
                Assert.Equal("Cloudflare", loaded.Results[0].Target);
                Assert.True(loaded.Results[0].IsRecommended);
            } finally {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Ensures snapshots from future schema versions are rejected with a clear compatibility error.
        /// </summary>
        [Fact]
        public void ResolverScoreStore_LoadFutureSchemaVersion_FailsClearly() {
            string path = Path.GetTempFileName();

            try {
                var snapshot = new ResolverScoreSnapshot {
                    SchemaVersion = ResolverScoreSnapshot.CurrentSchemaVersion + 1,
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Probe,
                        RecommendationAvailable = true,
                        RecommendedTarget = "Cloudflare"
                    }
                };
                var serializerOptions = new JsonSerializerOptions {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                serializerOptions.Converters.Add(new JsonStringEnumConverter());
                File.WriteAllText(path, JsonSerializer.Serialize(snapshot, serializerOptions));

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ResolverScoreStore.Load(path));

                Assert.Contains("newer DnsClientX release", exception.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains((ResolverScoreSnapshot.CurrentSchemaVersion + 1).ToString(), exception.Message, StringComparison.Ordinal);
            } finally {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Ensures resolver selection checks snapshot compatibility before attempting recommendation reuse.
        /// </summary>
        [Fact]
        public void ResolverScoreSelector_FutureSchemaVersion_FailsBeforeSelection() {
            bool selected = ResolverScoreSelector.TrySelectRecommended(
                new ResolverScoreSnapshot {
                    SchemaVersion = ResolverScoreSnapshot.CurrentSchemaVersion + 1,
                    Summary = new ResolverScoreSummary {
                        Mode = ResolverScoreMode.Benchmark,
                        RecommendationAvailable = true,
                        RecommendedTarget = "Cloudflare"
                    }
                },
                out ResolverSelectionResult? selection,
                out string? error);

            Assert.False(selected);
            Assert.Null(selection);
            Assert.Contains("newer DnsClientX release", error, StringComparison.OrdinalIgnoreCase);
        }
    }
}
