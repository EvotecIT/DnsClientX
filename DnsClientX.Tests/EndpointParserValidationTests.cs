using System;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Additional validation tests for EndpointParser.
    /// </summary>
    public class EndpointParserValidationTests {
        [Fact]
        public void EmptyString_ReportsError() {
            var eps = EndpointParser.TryParseMany(new [] { "" }, out var errors);
            Assert.Single(errors);
            Assert.Empty(eps);
        }

        [Fact]
        public void Hostname_WithoutPort_DefaultsTo53Udp() {
            var eps = EndpointParser.TryParseMany(new [] { "dns.google" }, out var errors);
            Assert.Empty(errors);
            Assert.Single(eps);
            Assert.Equal(53, eps[0].Port);
            Assert.Equal(Transport.Udp, eps[0].Transport);
        }

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

