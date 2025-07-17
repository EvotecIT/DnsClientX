using System;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests the <see cref="ClientX.MeasureLatencyAsync"/> helper method.
    /// </summary>
    public class MeasureLatencyTests {
        /// <summary>
        /// Ensures the measured latency is greater than zero.
        /// </summary>
        [Fact]
        public async Task MeasureLatency_ReturnsPositiveTime() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var latency = await client.MeasureLatencyAsync();
            Assert.True(latency > TimeSpan.Zero);
        }
    }
}
