using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for <see cref="EndpointParser"/> verifying supported resolver endpoint formats.
    /// </summary>
    public class DnsEndpointParserTests {
        /// <summary>
        /// Ensures IPv4, IPv6, hostname and DoH URL inputs parse into endpoints without errors.
        /// </summary>
        [Fact]
        public void TryParseMany_ParsesVariousFormats() {
            string[] inputs = new[] {
                "1.1.1.1:53",
                "[2606:4700:4700::1111]:53",
                "dns.google:53",
                "https://dns.google/dns-query"
            };

            var endpoints = EndpointParser.TryParseMany(inputs, out var errors);
            Assert.Empty(errors);
            Assert.Equal(4, endpoints.Length);

            Assert.Equal("1.1.1.1", endpoints[0].Host);
            Assert.Equal(53, endpoints[0].Port);
            Assert.Equal(Transport.Udp, endpoints[0].Transport);

            Assert.Equal("2606:4700:4700::1111", endpoints[1].Host);
            Assert.Equal(53, endpoints[1].Port);
            Assert.Equal(Transport.Udp, endpoints[1].Transport);

            Assert.Equal("dns.google", endpoints[2].Host);
            Assert.Equal(53, endpoints[2].Port);
            Assert.Equal(Transport.Udp, endpoints[2].Transport);

            Assert.NotNull(endpoints[3].DohUrl);
            Assert.Equal(Transport.Doh, endpoints[3].Transport);
        }
    }
}
