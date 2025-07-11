using System;
using System.Threading.Tasks;
using Xunit;

namespace DnsClientX.Tests {
    public class MeasureLatencyTests {
        [Fact]
        public async Task MeasureLatency_ReturnsPositiveTime() {
            using var client = new ClientX(DnsEndpoint.Cloudflare);
            var latency = await client.MeasureLatencyAsync();
            Assert.True(latency > TimeSpan.Zero);
        }
    }
}
