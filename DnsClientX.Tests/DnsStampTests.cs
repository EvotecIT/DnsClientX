using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests DNS stamp parsing and generation.
    /// </summary>
    public class DnsStampTests {
        /// <summary>
        /// Ensures plain DNS stamps round-trip through the endpoint model.
        /// </summary>
        [Fact]
        public void PlainDnsStamp_RoundTripsEndpoint() {
            var endpoint = new DnsResolverEndpoint {
                Host = "1.1.1.1",
                Port = 53,
                Transport = Transport.Udp,
                RequestFormat = DnsRequestFormat.DnsOverUDP,
                DnsSecOk = true
            };

            string stamp = DnsStamp.Create(endpoint);
            DnsResolverEndpoint parsed = DnsStamp.Parse(stamp);

            Assert.Equal(Transport.Udp, parsed.Transport);
            Assert.Equal(DnsRequestFormat.DnsOverUDP, parsed.RequestFormat);
            Assert.Equal("1.1.1.1", parsed.Host);
            Assert.Equal(53, parsed.Port);
            Assert.True(parsed.DnsSecOk);
        }

        /// <summary>
        /// Ensures known DoH stamps parse into HTTPS resolver endpoints.
        /// </summary>
        [Fact]
        public void DohStamp_ParsesKnownCloudflareExample() {
            const string stamp = "sdns://AgUAAAAAAAAABzEuMS4xLjEAGm1vemlsbGEuY2xvdWRmbGFyZS1kbnMuY29tCi9kbnMtcXVlcnk";

            DnsResolverEndpoint endpoint = DnsStamp.Parse(stamp);

            Assert.Equal(Transport.Doh, endpoint.Transport);
            Assert.Equal(DnsRequestFormat.DnsOverHttps, endpoint.RequestFormat);
            Assert.Equal("mozilla.cloudflare-dns.com", endpoint.Host);
            Assert.Equal(443, endpoint.Port);
            Assert.Equal(new Uri("https://mozilla.cloudflare-dns.com/dns-query"), endpoint.DohUrl);
            Assert.True(endpoint.DnsSecOk);
        }

        /// <summary>
        /// Ensures DoT and DoQ stamps round-trip with non-default ports.
        /// </summary>
        [Theory]
        [InlineData(Transport.Dot, DnsRequestFormat.DnsOverTLS, 853)]
        [InlineData(Transport.Quic, DnsRequestFormat.DnsOverQuic, 8853)]
        public void SecureTransportStamp_RoundTripsEndpoint(Transport transport, DnsRequestFormat requestFormat, int port) {
            var endpoint = new DnsResolverEndpoint {
                Host = "dns.example.test",
                Port = port,
                Transport = transport,
                RequestFormat = requestFormat
            };

            DnsResolverEndpoint parsed = DnsStamp.Parse(DnsStamp.Create(endpoint));

            Assert.Equal(transport, parsed.Transport);
            Assert.Equal(requestFormat, parsed.RequestFormat);
            Assert.Equal("dns.example.test", parsed.Host);
            Assert.Equal(port, parsed.Port);
        }

        /// <summary>
        /// Ensures endpoint parser accepts DNS stamps as resolver inputs.
        /// </summary>
        [Fact]
        public void EndpointParser_AcceptsDnsStampInput() {
            var endpoint = new DnsResolverEndpoint {
                Host = "9.9.9.9",
                Port = 53,
                Transport = Transport.Udp,
                RequestFormat = DnsRequestFormat.DnsOverUDP
            };

            DnsResolverEndpoint[] endpoints = EndpointParser.TryParseMany(new[] { DnsStamp.Create(endpoint) }, out var errors);

            Assert.Empty(errors);
            DnsResolverEndpoint parsed = Assert.Single(endpoints);
            Assert.Equal("9.9.9.9", parsed.Host);
            Assert.Equal(Transport.Udp, parsed.Transport);
        }

        /// <summary>
        /// Ensures unsupported stamp protocols fail with a descriptive error.
        /// </summary>
        [Fact]
        public void TryParse_UnsupportedDnsCryptStamp_ReturnsError() {
            const string dnsCryptPayload = "AQAAAAAAAAAAAQAAAAEAAAA";
            bool parsed = DnsStamp.TryParse("sdns://" + dnsCryptPayload, out DnsResolverEndpoint? endpoint, out string? error);

            Assert.False(parsed);
            Assert.Null(endpoint);
            Assert.Contains("DNSCrypt", error, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures invalid stamps are surfaced as endpoint parser errors.
        /// </summary>
        [Fact]
        public void EndpointParser_InvalidDnsStampReportsError() {
            DnsResolverEndpoint[] endpoints = EndpointParser.TryParseMany(new[] { "sdns://not-valid" }, out var errors);

            Assert.Empty(endpoints);
            string error = Assert.Single(errors);
            Assert.Contains("Invalid DNS stamp", error, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures generated DoH stamps preserve resolver paths and query strings.
        /// </summary>
        [Fact]
        public void DohStamp_RoundTripsPathAndQuery() {
            var endpoint = new DnsResolverEndpoint {
                Transport = Transport.Doh,
                RequestFormat = DnsRequestFormat.DnsOverHttps,
                Host = "dns.example.test",
                Port = 8443,
                DohUrl = new Uri("https://dns.example.test:8443/custom-query?token=abc")
            };

            DnsResolverEndpoint parsed = DnsStamp.Parse(DnsStamp.Create(endpoint));

            Assert.Equal("dns.example.test", parsed.Host);
            Assert.Equal(8443, parsed.Port);
            Assert.Equal(new Uri("https://dns.example.test:8443/custom-query?token=abc"), parsed.DohUrl);
        }

        /// <summary>
        /// Ensures explicit empty bootstrap address sets are accepted when parsing external stamps.
        /// </summary>
        [Fact]
        public void DohStamp_WithExplicitEmptyBootstrapSet_Parses() {
            var endpoint = new DnsResolverEndpoint {
                Transport = Transport.Doh,
                RequestFormat = DnsRequestFormat.DnsOverHttps,
                Host = "dns.example.test",
                Port = 443,
                DohUrl = new Uri("https://dns.example.test/dns-query")
            };

            string stamp = AppendPayloadByte(DnsStamp.Create(endpoint), 0);
            DnsResolverEndpoint parsed = DnsStamp.Parse(stamp);

            Assert.Equal(Transport.Doh, parsed.Transport);
            Assert.Equal("dns.example.test", parsed.Host);
            Assert.Equal(new Uri("https://dns.example.test/dns-query"), parsed.DohUrl);
        }

        /// <summary>
        /// Ensures DoH stamps preserve a non-default address port when hostname carries certificate name only.
        /// </summary>
        [Fact]
        public void DohStamp_UsesAddressPortWhenHostnameHasNoPort() {
            string stamp = CreateDohStamp("1.1.1.1:8443", "dns.example.test", "/dns-query");

            DnsResolverEndpoint parsed = DnsStamp.Parse(stamp);

            Assert.Equal(Transport.Doh, parsed.Transport);
            Assert.Equal("dns.example.test", parsed.Host);
            Assert.Equal(8443, parsed.Port);
            Assert.Equal(new Uri("https://dns.example.test:8443/dns-query"), parsed.DohUrl);
        }

        /// <summary>
        /// Ensures DoT and DoQ stamps preserve a non-default address port when hostname carries certificate name only.
        /// </summary>
        [Theory]
        [InlineData(0x03, Transport.Dot, DnsRequestFormat.DnsOverTLS)]
        [InlineData(0x04, Transport.Quic, DnsRequestFormat.DnsOverQuic)]
        public void TlsLikeStamp_UsesAddressPortWhenHostnameHasNoPort(byte protocol, Transport transport, DnsRequestFormat requestFormat) {
            string stamp = CreateTlsLikeStamp(protocol, "9.9.9.9:8853", "dns.example.test");

            DnsResolverEndpoint parsed = DnsStamp.Parse(stamp);

            Assert.Equal(transport, parsed.Transport);
            Assert.Equal(requestFormat, parsed.RequestFormat);
            Assert.Equal("dns.example.test", parsed.Host);
            Assert.Equal(8853, parsed.Port);
        }

        private static string AppendPayloadByte(string stamp, byte value) {
            const string scheme = "sdns://";
            string encoded = stamp.Substring(scheme.Length);
            string base64 = encoded.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            byte[] payload = Convert.FromBase64String(base64);
            byte[] updated = payload.Concat(new[] { value }).ToArray();
            return scheme + Convert.ToBase64String(updated)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string CreateDohStamp(string address, string host, string path) {
            using var stream = new MemoryStream();
            stream.WriteByte(0x02);
            WriteProperties(stream);
            WriteLengthPrefixed(stream, address);
            WriteEmptyVariableLengthSet(stream);
            WriteLengthPrefixed(stream, host);
            WriteLengthPrefixed(stream, path);
            return EncodePayload(stream.ToArray());
        }

        private static string CreateTlsLikeStamp(byte protocol, string address, string host) {
            using var stream = new MemoryStream();
            stream.WriteByte(protocol);
            WriteProperties(stream);
            WriteLengthPrefixed(stream, address);
            WriteEmptyVariableLengthSet(stream);
            WriteLengthPrefixed(stream, host);
            return EncodePayload(stream.ToArray());
        }

        private static void WriteProperties(Stream stream) {
            stream.Write(new byte[8], 0, 8);
        }

        private static void WriteLengthPrefixed(Stream stream, string value) {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteEmptyVariableLengthSet(Stream stream) {
            stream.WriteByte(0);
        }

        private static string EncodePayload(byte[] payload) {
            const string scheme = "sdns://";
            return scheme + Convert.ToBase64String(payload)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
