using System;
using Xunit;
using DnsClientX;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for extension methods on <see cref="DnsEndpoint"/>.
    /// </summary>
    public class DnsEndpointDescriptionTests {
        /// <summary>
        /// Ensures each endpoint has a non-empty description.
        /// </summary>
        [Fact]
        public void AllEndpointsHaveDescriptions() {
            foreach (DnsEndpoint ep in Enum.GetValues<DnsEndpoint>()) {
                string desc = ep.GetDescription();
                Assert.False(string.IsNullOrWhiteSpace(desc));
            }
        }
    }
}
