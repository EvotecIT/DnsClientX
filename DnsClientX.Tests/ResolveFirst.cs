namespace DnsClientX.Tests {
    public class ResolveFirst : NetworkTestBase {
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
        public async Task ShouldWorkForTXT(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            var answer = await Client.ResolveFirst("github.com", DnsRecordType.TXT);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "github.com");
            Assert.True(answer.Value.Type == DnsRecordType.TXT);
            Assert.True(answer.Value.Data.Length > 0);
        }

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
        public async Task ShouldWorkForA(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            var answer = await Client.ResolveFirst("evotec.pl", DnsRecordType.A);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "evotec.pl");
            Assert.True(answer.Value.Type == DnsRecordType.A);
        }

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
        public void ShouldWorkForTXT_Sync(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            var answer = Client.ResolveFirstSync("github.com", DnsRecordType.TXT);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "github.com");
            Assert.True(answer.Value.Type == DnsRecordType.TXT);
            Assert.True(answer.Value.Data.Length > 0);
        }

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
        public void ShouldWorkForA_Sync(DnsEndpoint endpoint) {
            using var Client = new ClientX(endpoint);
            var answer = Client.ResolveFirstSync("evotec.pl", DnsRecordType.A);
            Assert.True(answer != null);
            Assert.True(answer.Value.Name == "evotec.pl");
            Assert.True(answer.Value.Type == DnsRecordType.A);
        }
    }
}
