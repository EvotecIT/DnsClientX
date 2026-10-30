using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Additional validation tests for EndpointParser.
    /// </summary>
    public class EndpointParserValidationTests {
        /// <summary>
        /// Empty endpoint strings should be rejected with an error.
        /// </summary>
        [Fact]
        public void EmptyString_ReportsError() {
            var eps = EndpointParser.TryParseMany(new [] { "" }, out var errors);
            Assert.Single(errors);
            Assert.Empty(eps);
        }

        /// <summary>
        /// Hostnames without an explicit port default to UDP:53.
        /// </summary>
        [Fact]
        public void Hostname_WithoutPort_DefaultsTo53Udp() {
            var eps = EndpointParser.TryParseMany(new [] { "dns.google" }, out var errors);
            Assert.Empty(errors);
            Assert.Single(eps);
            Assert.Equal(53, eps[0].Port);
            Assert.Equal(Transport.Udp, eps[0].Transport);
        }

    }
}

