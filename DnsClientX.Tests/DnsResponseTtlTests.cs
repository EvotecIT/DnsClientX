using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for TTL metric computations on <see cref="DnsResponse"/>.
    /// </summary>
    public class DnsResponseTtlTests {
        /// <summary>
        /// Computes TtlMin as minimum and TtlAvg as arithmetic mean.
        /// </summary>
        [Fact]
        public void ComputeTtlMetrics_ComputesMinAndAvg() {
            var response = new DnsResponse {
                Answers = new [] {
                    new DnsAnswer { Name = "a", Type = DnsRecordType.A, TTL = 60, DataRaw = "127.0.0.1" },
                    new DnsAnswer { Name = "a", Type = DnsRecordType.A, TTL = 120, DataRaw = "127.0.0.2" }
                }
            };
            response.ComputeTtlMetrics();
            Assert.Equal(60, response.TtlMin);
            Assert.Equal(90, Math.Round(response.TtlAvg!.Value));
        }

        /// <summary>
        /// Truncated property mirrors IsTruncated for convenience.
        /// </summary>
        [Fact]
        public void Truncated_Mirrors_IsTruncated() {
            var r = new DnsResponse { IsTruncated = true };
            Assert.True(r.Truncated);
        }
    }
}
