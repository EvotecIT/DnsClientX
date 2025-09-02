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
    }
}
