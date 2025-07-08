using System;
using Xunit;
using DnsClientX;

namespace DnsClientX.Tests {
    public class DnsEndpointDescriptionTests {
        [Fact]
        public void AllEndpointsHaveDescriptions() {
            foreach (DnsEndpoint ep in Enum.GetValues(typeof(DnsEndpoint))) {
                string desc = ep.GetDescription();
                Assert.False(string.IsNullOrWhiteSpace(desc));
            }
        }
    }
}
