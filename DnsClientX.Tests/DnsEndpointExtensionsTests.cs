using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="DnsEndpointExtensions"/> helper methods.
    /// </summary>
    public class DnsEndpointExtensionsTests {
        /// <summary>
        /// Verifies that all endpoints have descriptions.
        /// </summary>
        [Fact]
        public void GetAllWithDescriptions_ReturnsAllEndpoints() {
            var all = DnsEndpointExtensions.GetAllWithDescriptions().ToList();
            int expectedCount = Enum.GetValues<DnsEndpoint>().Length;
            Assert.Equal(expectedCount, all.Count);
            Assert.All(all, pair => Assert.False(string.IsNullOrWhiteSpace(pair.Description)));
        }
    }
}
