namespace DnsClientX.Tests {
    public class QueryDnsByEndpoint {
        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldWorkForTXT(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("github.com", DnsRecordType.TXT, endpoint).ConfigureAwait(false);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "github.com");
                Assert.True(answer.Type == DnsRecordType.TXT);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("evotec.pl", DnsRecordType.A, endpoint).ConfigureAwait(false);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "evotec.pl");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }

        [Theory]
        [InlineData(DnsEndpoint.System)]
        [InlineData(DnsEndpoint.SystemTcp)]
        [InlineData(DnsEndpoint.Cloudflare)]
        [InlineData(DnsEndpoint.CloudflareFamily)]
        [InlineData(DnsEndpoint.CloudflareSecurity)]
        [InlineData(DnsEndpoint.CloudflareWireFormat)]
        [InlineData(DnsEndpoint.CloudflareWireFormatPost)]
        [InlineData(DnsEndpoint.Google)]
        [InlineData(DnsEndpoint.GoogleWireFormat)]
        [InlineData(DnsEndpoint.GoogleWireFormatPost)]
        [InlineData(DnsEndpoint.OpenDNS)]
        [InlineData(DnsEndpoint.OpenDNSFamily)]
        public async Task ShouldWorkForPTR(DnsEndpoint endpoint) {
            var response = await ClientX.QueryDns("1.1.1.1", DnsRecordType.PTR, endpoint).ConfigureAwait(false);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Data == "one.one.one.one");
                Assert.True(answer.Name == "1.1.1.1.in-addr.arpa");
                Assert.True(answer.Type == DnsRecordType.PTR);
                Assert.True(answer.Data.Length > 0);
            }
        }
    }
}
