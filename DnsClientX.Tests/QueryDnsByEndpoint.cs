namespace DnsClientX.Tests {
    /// <summary>
    /// Integration tests verifying DNS queries against multiple endpoints.
    /// </summary>
    public class QueryDnsByEndpoint {
        /// <summary>
        /// Ensures TXT record queries succeed for the specified endpoint.
        /// </summary>
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
#endif
        [Theory]
        public async Task ShouldWorkForTXT(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, endpoint);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Ensures A record queries succeed for the specified endpoint.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
[InlineData(DnsEndpoint.GoogleWireFormatPost)]
[InlineData(DnsEndpoint.OpenDNS)]
[InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
[InlineData(DnsEndpoint.CloudflareQuic)]
[InlineData(DnsEndpoint.GoogleQuic)]
#endif
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, endpoint);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

        /// <summary>
        /// Ensures reverse PTR lookups succeed for the specified endpoint.
        /// </summary>
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.CloudflareOdoh)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
#if DNS_OVER_QUIC
        [InlineData(DnsEndpoint.CloudflareQuic)]
        [InlineData(DnsEndpoint.GoogleQuic)]
#endif
        public async Task ShouldWorkForPTR(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("1.1.1.1", DnsRecordType.PTR, endpoint);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Data == "one.one.one.one");
                Assert.True(answer.Name == "1.1.1.1.in-addr.arpa");
                Assert.True(answer.Type == DnsRecordType.PTR);
                Assert.True(answer.Data.Length > 0);
            }
        }
    }
}
