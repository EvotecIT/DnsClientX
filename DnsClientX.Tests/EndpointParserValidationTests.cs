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

        /// <summary>
        /// A valid DoH HTTPS URL parses into a DoH endpoint with correct host.
        /// </summary>
        [Fact]
        public void DohHttps_Parses() {
            var eps = EndpointParser.TryParseMany(new [] { "https://dns.google/dns-query" }, out var errors);
            Assert.Empty(errors);
            Assert.Single(eps);
            Assert.Equal(Transport.Doh, eps[0].Transport);
            Assert.Equal("dns.google", eps[0].Host);
        }
    }
}

