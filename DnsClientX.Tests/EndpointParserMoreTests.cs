using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Additional tests for EndpointParser covering error cases and validation.
    /// </summary>
    public class EndpointParserMoreTests {

        /// <summary>Rejects insecure URLs, malformed IPv6, invalid ports and unknown transports.</summary>
        [Theory]
        [InlineData("http://dns.google/dns-query", "Unsupported scheme")]
        [InlineData("[2001:4860:4860::8888:53", "Invalid IPv6 endpoint")]
        [InlineData("1.1.1.1:99999", "Invalid port")]
        [InlineData("smtp@dns.google:53", "Unsupported transport prefix")]
        public void TryParseMany_InvalidEndpoint_ReportsSpecificError(string input, string expectedError) {
            var endpoints = EndpointParser.TryParseMany(new[] { input }, out var errors);
            Assert.Contains(expectedError, Assert.Single(errors), StringComparison.OrdinalIgnoreCase);
            Assert.Empty(endpoints);
        }

        /// <summary>
        /// Custom DoH endpoints should preserve an explicit non-default port when constructing the query URI.
        /// </summary>
        [Fact]
        public void BuildDohUri_PreservesCustomPort() {
            var endpoints = EndpointParser.TryParseMany(new[] { "doh@resolver.example:8443" }, out var errors);

            Assert.Empty(errors);
            Uri uri = EndpointParser.BuildDohUri(endpoints.Single());

            Assert.Equal("https://resolver.example:8443/dns-query", uri.AbsoluteUri);
        }

        /// <summary>
        /// Custom DoH3 endpoints should preserve their explicit request format when using host-based inputs.
        /// </summary>
        [Fact]
        public void TryParseMany_Doh3HostInput_PreservesHttp3Format() {
            var endpoints = EndpointParser.TryParseMany(new[] { "doh3@dns.quad9.net:443" }, out var errors);

            Assert.Empty(errors);
            Assert.Single(endpoints);
            Assert.Equal(Transport.Doh, endpoints[0].Transport);
            Assert.Equal(DnsRequestFormat.DnsOverHttp3, endpoints[0].RequestFormat);
            Assert.Equal("dns.quad9.net", endpoints[0].Host);
        }
    }
}
