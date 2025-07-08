using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    public class DnsEndpointExtensionsTests {
        [Fact]
        public void GetAllWithDescriptions_ReturnsAllEndpoints() {
            var all = DnsEndpointExtensions.GetAllWithDescriptions().ToList();
            int expectedCount = Enum.GetValues(typeof(DnsEndpoint)).Length;
            Assert.Equal(expectedCount, all.Count);
            Assert.All(all, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Description)));
        }
    }
}
