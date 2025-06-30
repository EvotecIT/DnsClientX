namespace DnsClientX.Tests {
    public class QueryDnsIDN {
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
            var response = await ClientX.QueryDns("www.bücher.de", DnsRecordType.A, endpoint).ConfigureAwait(false);
            foreach (DnsAnswer answer in response.Answers) {
                Assert.True(answer.Name == "www.xn--bcher-kva.de");
                Assert.True(answer.Type == DnsRecordType.A);
                Assert.True(answer.Data.Length > 0);
            }
        }
    }
}
