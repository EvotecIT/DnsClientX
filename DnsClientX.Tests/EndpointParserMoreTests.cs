using System;
using System.Linq;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Additional tests for EndpointParser covering error cases and validation.
    /// </summary>
    public class EndpointParserMoreTests {
        /// <summary>
        /// Invalid (non-HTTPS) scheme in DoH URL should be rejected.
        /// </summary>
        [Fact]
        public void TryParseMany_InvalidScheme_ShouldReportError() {
            var inputs = new[] { "http://dns.google/dns-query" }; // only https is allowed
            var eps = EndpointParser.TryParseMany(inputs, out var errors);
            Assert.Single(errors);
            Assert.Contains("Unsupported scheme", errors[0], StringComparison.OrdinalIgnoreCase);
            Assert.Empty(eps);
        }

        /// <summary>
        /// Malformed IPv6 with brackets should be reported as invalid.
        /// </summary>
        [Fact]
        public void TryParseMany_InvalidIpv6Format_ShouldReportError() {
            var inputs = new[] { "[2001:4860:4860::8888:53" }; // missing closing bracket
            _ = EndpointParser.TryParseMany(inputs, out var errors);
            Assert.Single(errors);
        }

        /// <summary>
        /// Invalid port number should be reported as an error.
        /// </summary>
        [Fact]
        public void TryParseMany_InvalidPort_ShouldReportError() {
            var inputs = new[] { "1.1.1.1:99999" };
            _ = EndpointParser.TryParseMany(inputs, out var errors);
            Assert.Single(errors);
        }

        /// <summary>
        /// Unsupported explicit transport prefixes should be reported as errors.
        /// </summary>
        [Fact]
        public void TryParseMany_InvalidTransportPrefix_ShouldReportError() {
            var inputs = new[] { "smtp@dns.google:53" };
            _ = EndpointParser.TryParseMany(inputs, out var errors);
            Assert.Single(errors);
            Assert.Contains("Unsupported transport prefix", errors[0], StringComparison.OrdinalIgnoreCase);
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
